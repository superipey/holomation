#include <ESP8266WiFi.h>
#include <PubSubClient.h>

//const char* ssid = "cyber@superipey";
//const char* password = "ipeyganteng";

const char* ssid = "Hotspot LSKK";
const char* password = "lskkhotspot";

//char* mqtt_server = "192.168.137.1";
char* mqtt_server = "167.205.7.226";

WiFiClient espClient;
PubSubClient client(espClient);
long lastMsg = 0;
char msg[50];
int value = 0;

String inputString = "";
boolean stringComplete = false;

extern void serialEventRun(void) __attribute__((weak));

void setup() {
  Serial.begin(115200);
  setup_wifi();
  client.setServer(mqtt_server, 1883);
  client.setCallback(callback);

  inputString.reserve(50);

  pinMode(D1, OUTPUT);
  pinMode(D2, OUTPUT);
  digitalWrite(D1, 1);
  digitalWrite(D2, 1);  
}

void resetup() {
  client.disconnect();
  client.setServer(mqtt_server, 1883);
  reconnect();
}

void loop() {
  if (!client.connected()) {
    reconnect();
  }

  client.loop();
}

void setup_wifi() {
  delay(10);
  
  Serial.println();
  Serial.print("Connecting to ");
  Serial.println(ssid);

  WiFi.begin(ssid, password);

  while (WiFi.status() != WL_CONNECTED) {
    delay(500);
    Serial.print(".");
  }

  Serial.println("");
  Serial.println("WiFi connected");
  Serial.println("IP address: ");
  Serial.println(WiFi.localIP());
}

void reconnect() {
  while (!client.connected()) {
    Serial.print("Attempting MQTT connection...");
    if (client.connect("TMDG2017-5-Fertra", "/ARX:ARmachine", "12345")) {
      Serial.println("Connected");

      client.publish("TMDG2017-5-Fertra", "Helo, I'm superipey", true);
      client.subscribe("led1");
      client.subscribe("led2");
    } else {
      Serial.print("failed, rc=");
      Serial.print(client.state());
      Serial.println(" try again in 3 seconds");
      delay(3000);
    }
  }
}

// ==============================================================
// Function callback
// ==============================================================
void callback(char* topic, byte* payload, unsigned int length) {
  Serial.print("Message arrived [");
  Serial.print(topic);
  Serial.print("] ");
  for (int i = 0; i < length; i++) {
    Serial.print((char)payload[i]);
  }
  Serial.println();

  String txtTopic(topic);

  if (txtTopic == "led1") {
    if ((char)payload[0] == '1') {
      digitalWrite(D1, 1); 
    }

    if ((char)payload[0] == '0') {
      digitalWrite(D1, 0);
    }
  }

  if (txtTopic == "led2") {
    if ((char)payload[0] == '1') {
      digitalWrite(D2, 1); 
    }

    if ((char)payload[0] == '0') {
      digitalWrite(D2, 0);
    }
  }
}

// ==================
// Another Function
// ==================
String getValue(String data, char separator, int index)
{
    int found = 0;
    int strIndex[] = { 0, -1 };
    int maxIndex = data.length() - 1;

    for (int i = 0; i <= maxIndex && found <= index; i++) {
        if (data.charAt(i) == separator || i == maxIndex) {
            found++;
            strIndex[0] = strIndex[1] + 1;
            strIndex[1] = (i == maxIndex) ? i+1 : i;
        }
    }
    return found > index ? data.substring(strIndex[0], strIndex[1]) : "";
}

void serialEvent() {
  Serial.print("receive");
  while (Serial.available()) {
    char inChar = (char) Serial.read();
    inputString += inChar;
    if (inChar == ';') {
      stringComplete = true;
    }
  }
}
