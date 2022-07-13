# Mqtt Subscriber

This sample module lets you subscribe to Mqtt topics on a (local) broker and send the data via IoT Edge to an Azure IoT Hub.

## Scenarios

This IoT Edge module can be used as a replacement for the 1.2 preview MQTT broker functunality (in combination with e.g. Mosquitto).

## TODO

- add authentication to the MQTT broker

## Configuration

### Module Twin

The module twin is used to configure the connection to a local MQTT broker.

| Property | Type | Required | Description |
| :------: | :--: | :------: | :---------- |
| `MqttServer` | string | Yes | Hostname of the local MQTT broker |
| `MqttPort` | integer | No | Port of the local MQTT broker. 1883 by default |
| `MqttTopics` | string | Yes | Comma separated list of topics to subscribe to |
| `MqttUser` | string | No | Username for the local MQTT broker |
| `MqttPassword` | string | No | Password for the local MQTT broker |

## recommended improvements

Since this is just a sample, I could ommit some best practices (and leave them to you (to do a pull request)) ;-)

- [Edge Secret Management](https://github.com/vslepakov/edge-secrets) - to store the MQTT password in a secure way
