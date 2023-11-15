using Microsoft.Azure.Devices.Client;
using Microsoft.Azure.Devices.Client.Transport.Mqtt;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using System;
using System.Runtime.Loader;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using MQTTnet.Client;
using MQTTnet.Extensions.ManagedClient;
using MQTTnet;
using MQTTnet.Packets;
using System.Collections.Generic;
using System.Linq;

namespace MqttSubscriberModule
{
    internal class Program
    {
        static int counter;
        private static string _mqttServer;
        private static int _mqttServerPort = 1883;
        private static string _mqttUsername;
        private static string _mqttPassword;
        private static string[] _mqttTopics;
        private static string _mqttMessageTemplate = "{\"[topic]\":[message]}";
        private static MqttFactory _mqttFactory;
        private static IManagedMqttClient _managedMqttClient;
        private static ModuleClient _ioTHubModuleClient;

        static void Main(string[] args)
        {
            Init().Wait();

            // Wait until the app unloads or is cancelled
            var cts = new CancellationTokenSource();
            AssemblyLoadContext.Default.Unloading += (ctx) => cts.Cancel();
            Console.CancelKeyPress += (sender, cpe) => cts.Cancel();
            WhenCancelled(cts.Token).Wait();
        }

        /// <summary>
        /// Handles cleanup operations when app is cancelled or unloads
        /// </summary>
        public static Task WhenCancelled(CancellationToken cancellationToken)
        {
            var tcs = new TaskCompletionSource<bool>();
            cancellationToken.Register(s => ((TaskCompletionSource<bool>)s).SetResult(true), tcs);
            return tcs.Task;
        }

        /// <summary>
        /// Initializes the ModuleClient and sets up the callback to receive
        /// messages containing temperature information
        /// </summary>
        static async Task Init()
        {
            MqttTransportSettings mqttSetting = new MqttTransportSettings(TransportType.Mqtt_Tcp_Only);
            ITransportSettings[] settings = { mqttSetting };

            // Open a connection to the Edge runtime
            _ioTHubModuleClient = await ModuleClient.CreateFromEnvironmentAsync(settings);
            await _ioTHubModuleClient.OpenAsync();
            Console.WriteLine("IoT Hub module client initialized.");

            // initialize Mqtt client to connect to a local broker
            _mqttFactory = new MqttFactory();

            // Read Mqtt settings from the module twin's desired properties
            var moduleTwin = await _ioTHubModuleClient.GetTwinAsync();
            await OnDesiredPropertiesUpdate(moduleTwin.Properties.Desired, _ioTHubModuleClient);
            // Attach a callback for updates to the module twin's desired properties.
            await _ioTHubModuleClient.SetDesiredPropertyUpdateCallbackAsync(OnDesiredPropertiesUpdate, null);
        }

        static async Task OnDesiredPropertiesUpdate(TwinCollection desiredProperties, object userContext)
        {
            try
            {
                Console.WriteLine("Desired property change:");
                Console.WriteLine(JsonConvert.SerializeObject(desiredProperties));

                _mqttUsername = desiredProperties["MqttUser"]?.Value;
                _mqttPassword = desiredProperties["MqttPassword"]?.Value;
                if (desiredProperties["MqttServer"] != null)
                {
                    _mqttServer = desiredProperties["MqttServer"].Value;
                    // (re)connect to the MQTT broker with optional authentication
                    await ConnectToMqttBroker();
                }

                if (desiredProperties["MqttTopics"] != null)
                {
                    string[] topics = JsonConvert.DeserializeObject<string[]>(desiredProperties["MqttTopics"].Value);
                    // subscribe to topics
                    await Subscribe_Topic(topics);
                }

                if (desiredProperties["MqttMessageTemplate"] != null)
                {
                    // apply message template
                    _mqttMessageTemplate = desiredProperties["MqttMessageTemplate"].Value;
                }
            }
            catch (AggregateException ex)
            {
                foreach (Exception exception in ex.InnerExceptions)
                {
                    Console.WriteLine();
                    Console.WriteLine("Error when receiving desired property: {0}", exception);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine("Error when receiving desired property: {0}", ex.Message);
            }
        }

        /// <summary>
        /// Connect to MQTT broker
        /// </summary>
        /// <returns></returns>
        static async Task ConnectToMqttBroker()
        {
            _managedMqttClient = _mqttFactory.CreateManagedMqttClient();

            var mqttClientOptions = new MqttClientOptionsBuilder()
                .WithTcpServer(_mqttServer, _mqttServerPort);
            if (_mqttUsername != null && _mqttPassword != null)
                mqttClientOptions.WithCredentials(_mqttUsername, _mqttPassword);
            mqttClientOptions.Build();

            var managedMqttClientOptions = new ManagedMqttClientOptionsBuilder()
                .WithClientOptions(mqttClientOptions)
                .Build();

            await _managedMqttClient.StartAsync(managedMqttClientOptions);
            _managedMqttClient.ConnectingFailedAsync += (arg) =>
            {
                Console.WriteLine($"Failed to connect to MQTT broker '{_mqttServer}:{_mqttServerPort}': {arg.Exception.Message}, {arg.Exception.InnerException?.Message}");
                return Task.CompletedTask;
            };
            _managedMqttClient.ConnectionStateChangedAsync += (arg) =>
            {
                Console.WriteLine($"MQTT broker '{_mqttServer}:{_mqttServerPort}' connection state changed");
                return Task.CompletedTask;
            };

            _managedMqttClient.ApplicationMessageReceivedAsync += OnMessageReceived;

            Console.WriteLine("The managed MQTT client is connected.");
        }

        private static Task OnMessageReceived(MqttApplicationMessageReceivedEventArgs arg)
        {
            Console.WriteLine("Received application message.");
            var topic = arg.ApplicationMessage.Topic;
            var payload = Encoding.Default.GetString(arg.ApplicationMessage.PayloadSegment.Array ?? new byte[0]);

            Console.WriteLine($"Received on '{topic}': '{payload}'");

            var messageBody = _mqttMessageTemplate.Replace("[topic]", topic).Replace("[message]", payload);
            SendMessage(messageBody).Wait();

            return Task.CompletedTask;
        }

        public static async Task Subscribe_Topic(string[] topics)
        {
            if (_mqttTopics != null)
            {
                // unsubscribe first
                await _managedMqttClient.UnsubscribeAsync(_mqttTopics);
            }
            _mqttTopics = topics;

            // subscribe to desired topics
            var topicFilter = new List<MqttTopicFilter>();
            topics.Select(t => new MqttTopicFilter { Topic = t }).ToList().ForEach(t => topicFilter.Add(t));

            await _managedMqttClient.SubscribeAsync(topicFilter);

            Console.WriteLine("MQTT client subscribed to topic.");
        }

        /// <summary>
        /// This method is called whenever the module is sent a message from the EdgeHub. 
        /// It just pipe the messages without any change.
        /// It prints all the incoming messages.
        /// </summary>
        static async Task<MessageResponse> SendMessage(string messageString)
        {
            int counterValue = Interlocked.Increment(ref counter);

            if (!string.IsNullOrEmpty(messageString))
            {
                Console.WriteLine($"Received message: {counterValue}, Body: [{messageString}]");

                var messageBytes = Encoding.UTF8.GetBytes(messageString);
                using (var message = new Message(messageBytes))
                {
                    message.MessageId = Guid.NewGuid().ToString();
                    message.ContentEncoding = "utf-8";
                    message.ContentType = "application/json";
                    await _ioTHubModuleClient.SendEventAsync("mqtt", message);

                    Console.WriteLine($"Message sent: {counterValue}");
                }
            }
            return MessageResponse.Completed;
        }
    }
}
