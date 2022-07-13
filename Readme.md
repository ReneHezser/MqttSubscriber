# Mqtt Subscriber

This sample module lets you subscribe to Mqtt topics on a (local) broker and send the data via IoT Edge to an Azure IoT Hub.

## Scenarios

This IoT Edge module can be used as a replacement for the 1.2 preview MQTT broker functunality (in combination with e.g. Mosquitto).

## Configuration

Following IoT Edge principals, the configuration is done on device/module level by setting the module twin. To change them, or view transmitted data, you can use the [IoT Explorer](https://github.com/Azure/azure-iot-explorer).

![IoT Explorer - Module Twin](/assets/IoT-Explorer-Module-Twin.jpg)

### Module Twin

The module twin is used to configure the connection to a local MQTT broker.

| Property | Type | Required | Description |
| :------: | :--: | :------: | :---------- |
| `MqttServer` | string | Yes | Hostname of the local MQTT broker |
| `MqttPort` | integer | No | Port of the local MQTT broker. 1883 by default |
| `MqttTopics` | string | Yes | Comma separated list of topics to subscribe to |
| `MqttUser` | string | No | Username for the local MQTT broker |
| `MqttPassword` | string | No | Password for the local MQTT broker |
| `MqttMessageTemplate` | string | No | this property controlls the body of the message sent to IoT Hub. Default: `{\"[topic]\":[message]}` |

## recommended improvements

Since this is just a sample, I could ommit some best practices (and leave them to you (to do a pull request)) ;-)

- [Edge Secret Management](https://github.com/vslepakov/edge-secrets) - to store the MQTT password in a secure way
- optional read settings from local file instead of module twin (or in addition)