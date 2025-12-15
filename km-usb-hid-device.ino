// General
#include <Arduino.h>

// ESP32
#include <SPI.h>

// Ethernet
#include <ETH.h>

// SD Card
#include <SD.h>

// USB
#include "USB.h"
#include "USBHIDMouse.h"
#include "USBHIDKeyboard.h"

// HTTP / WS
#include <AsyncTCP.h>
#include <ESPAsyncWebServer.h>

/*
 * Ethernet
 */

// Define W5500 pin assignments
#ifndef ETH_PHY_CS
#define ETH_PHY_TYPE ETH_PHY_W5500
#define ETH_PHY_ADDR 1
#define ETH_PHY_CS 14   // W5500_CS
#define ETH_PHY_IRQ 10  // W5500_INT
#define ETH_PHY_RST 9   // W5500_RST
#define ETH_PHY_SPI_HOST SPI2_HOST
#define ETH_PHY_SPI_SCK 13   // W5500_SCK
#define ETH_PHY_SPI_MISO 12  // W5500_MISO
#define ETH_PHY_SPI_MOSI 11  // W5500_MOSI
#endif

// Static IP settings
bool useDhcp = false;
IPAddress ip(192, 168, 1, 177);
IPAddress gateway(192, 168, 1, 1);
IPAddress subnet(255, 255, 255, 0);
IPAddress dns(192, 168, 1, 1);

static bool ethConnected = false;

void onEvent(arduino_event_id_t event, arduino_event_info_t info) {
  switch (event) {
    case ARDUINO_EVENT_ETH_START:
      Serial.println("ETH Started");
      ETH.setHostname("km-usb-hid-device");
      break;
    case ARDUINO_EVENT_ETH_CONNECTED:
      Serial.println("ETH Connected");
      break;
    case ARDUINO_EVENT_ETH_GOT_IP:
      Serial.printf("ETH Got IP: '%s'\n", esp_netif_get_desc(info.got_ip.esp_netif));
      Serial.println(ETH);
      ethConnected = true;
      break;
    case ARDUINO_EVENT_ETH_LOST_IP:
      Serial.println("ETH Lost IP");
      ethConnected = false;
      break;
    case ARDUINO_EVENT_ETH_DISCONNECTED:
      Serial.println("ETH Disconnected");
      ethConnected = false;
      break;
    case ARDUINO_EVENT_ETH_STOP:
      Serial.println("ETH Stopped");
      ethConnected = false;
      break;
    default: break;
  }
}

/*
* SD Card
*/

#define SD_MISO_PIN 5
#define SD_MOSI_PIN 6
#define SD_SCLK_PIN 7
#define SD_CS_PIN 4

SPIClass spiSD(FSPI);

uint32_t cardSize;

/*
 * USB
 */

USBHIDMouse Mouse;
USBHIDKeyboard Keyboard;

/*
 * Config
 */

void parseIPAddress(const char *str, IPAddress &addr) {
  uint8_t parts[4] = { 0 };
  int part = 0;

  while (*str && part < 4) {
    if (*str == '.') {
      part++;
    } else if (*str >= '0' && *str <= '9') {
      parts[part] = parts[part] * 10 + (*str - '0');
    }
    str++;
  }
  addr = IPAddress(parts[0], parts[1], parts[2], parts[3]);
}

bool loadConfig() {
  Serial.println("Loading config...");

  File file = SD.open("/config.txt");
  if (!file) {
    Serial.println("Failed to open config.txt");
    return false;
  }

  char line[32];
  while (file.available()) {
    int i = 0;
    while (file.available() && i < sizeof(line) - 1) {
      char c = file.read();
      if (c == '\n' || c == '\r') {
        break;
      }
      line[i++] = c;
    }
    line[i] = '\0';

    if (i == 0) {
      continue;
    }

    char *eq = strchr(line, '=');
    if (!eq) {
      continue;
    }

    *eq = '\0';

    char *key = line;
    char *value = eq + 1;

    if (strcmp(key, "dhcp") == 0) {
      useDhcp = (strcmp(value, "true") == 0);
    } else if (strcmp(key, "ip") == 0) {
      parseIPAddress(value, ip);
    } else if (strcmp(key, "gw") == 0) {
      parseIPAddress(value, gateway);
    } else if (strcmp(key, "sn") == 0) {
      parseIPAddress(value, subnet);
    } else if (strcmp(key, "dns") == 0) {
      parseIPAddress(value, dns);
    }
  }

  file.close();

  Serial.println("Successfully loaded config");
  return true;
}

/*
 * HTTP / WS
 */

static AsyncWebServer server(80);

static AsyncWebSocketMessageHandler wsHandler;
static AsyncWebSocket ws("/ws", wsHandler.eventHandler());

static uint32_t lastWS = 0;
static uint32_t deltaWS = 2000;

static const char *htmlContent PROGMEM = R"(
<!DOCTYPE html>
<html>
<head>
  <title>WebSocket</title>
</head>
<body>
  <h1>WebSocket Example</h1>
  <>Open your browser console!</p>
  <input type="text" id="message" placeholder="Type a message">
  <button onclick='sendMessage()'>Send</button>
  <script>
    var ws = new WebSocket('ws://' + location.host + '/ws');
    ws.onopen = function() {
      console.log("WebSocket connected");
    };
    ws.onmessage = function(event) {
      console.log("WebSocket message: " + event.data);
    };
    ws.onclose = function() {
      console.log("WebSocket closed");
    };
    ws.onerror = function(error) {
      console.log("WebSocket error: " + error);
    };
    function sendMessage() {
      var message = document.getElementById("message").value;
      ws.send(message);
      console.log("WebSocket sent: " + message);
    }
  </script>
</body>
</html>
  )";
static const size_t htmlContentLength = strlen_P(htmlContent);

/*
 * Arduino
 */

void setup() {
  // Start serial and wait
  Serial.begin(115200);  // Start serial communication
  while (!Serial) {
    ;  // Wait for the serial port to be ready
  }

  Serial.println("===========================");
  Serial.println("= Initialization Started  =");
  Serial.println("===========================");

  /*
    * SD Card Setup
    */

  pinMode(SD_MISO_PIN, INPUT_PULLUP);
  spiSD.begin(SD_SCLK_PIN, SD_MISO_PIN, SD_MOSI_PIN, SD_CS_PIN);

  if (SD.begin(SD_CS_PIN, spiSD)) {
    Serial.println("SDCard MOUNT SUCCESS");
  } else {
    Serial.println("SDCard MOUNT FAIL");
  }

  cardSize = SD.cardSize() / (1024 * 1024);
  Serial.printf("SDCard Size: %d MB\n", cardSize);

  if (cardSize == 0) {
    Serial.println("No SD card detected, skip config load...");
  } else {
    loadConfig();
  }

  /*
     * Ethernet Setup
     */

  // Initialize SPI with specified pin configuration
  Network.onEvent(onEvent);
  ETH.begin(ETH_PHY_TYPE, ETH_PHY_ADDR, ETH_PHY_CS, ETH_PHY_IRQ, ETH_PHY_RST, ETH_PHY_SPI_HOST, ETH_PHY_SPI_SCK, ETH_PHY_SPI_MISO, ETH_PHY_SPI_MOSI);

  // Initialize Ethernet using DHCP to obtain an IP address
  if (useDhcp) {
    Serial.println("Using DHCP...");
  }
  else {
    Serial.println("Using static IP...");
    // Initialize with static IP
    ETH.config(ip, gateway, subnet, dns);
  }

  /*
    *  USB Setup
    */

  // Optional: customise USB strings (purely cosmetic)
  USB.productName("KM USB HID Device");
  USB.manufacturerName("Adam Brewer");
  USB.serialNumber("AKMUSBHID1");

  // Start HID mouse + HID keyboard, then USB device
  Mouse.begin();
  Keyboard.begin();
  USB.begin();
  Serial.println("Keyboard/Mouse/USB Initialized...");

  // serves root html page
  server.on("/", HTTP_GET, [](AsyncWebServerRequest *request) {
    request->send(200, "text/html", (const uint8_t *)htmlContent, htmlContentLength);
  });

  wsHandler.onConnect([](AsyncWebSocket *server, AsyncWebSocketClient *client) {
    Serial.printf("Client %" PRIu32 " connected\n", client->id());
    server->textAll("New client: " + String(client->id()));
  });

  wsHandler.onDisconnect([](AsyncWebSocket *server, uint32_t clientId) {
    Serial.printf("Client %" PRIu32 " disconnected\n", clientId);
    server->textAll("Client " + String(clientId) + " disconnected");
  });

  wsHandler.onError([](AsyncWebSocket *server, AsyncWebSocketClient *client, uint16_t errorCode, const char *reason, size_t len) {
    Serial.printf("Client %" PRIu32 " error: %" PRIu16 ": %s\n", client->id(), errorCode, reason);
  });

  wsHandler.onMessage([](AsyncWebSocket *server, AsyncWebSocketClient *client, const uint8_t *data, size_t len) {
    Serial.printf("Client %" PRIu32 " data: %s\n", client->id(), (const char *)data);
    server->textAll(data, len);
  });

  wsHandler.onFragment([](AsyncWebSocket *server, AsyncWebSocketClient *client, const AwsFrameInfo *frameInfo, const uint8_t *data, size_t len) {
    Serial.printf("Client %" PRIu32 " fragment %" PRIu32 ": %s\n", client->id(), frameInfo->num, (const char *)data);
  });

  server.addHandler(&ws);
  server.begin();
  Serial.println("Server started...");

  Serial.println("===========================");
  Serial.println("= Initialization Complete =");
  Serial.println("===========================");
}

void loop() {
  uint32_t now = millis();

  if (now - lastWS >= deltaWS) {
    ws.cleanupClients();
    lastWS = millis();
  }
}
