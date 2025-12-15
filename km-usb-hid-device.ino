// General
#include <Arduino.h>

// ESP32
#include <SPI.h>

// Ethernet
#include <Ethernet.h>

// SD Card
#include <SD.h>

// USB
#include "USB.h"
#include "USBHIDMouse.h"
#include "USBHIDKeyboard.h"

/*
 * Ethernet
 */

// Define W5500 pin assignments
#define W5500_CS    14  // Chip Select pin
#define W5500_RST    9  // Reset pin
#define W5500_INT   10  // Interrupt pin
#define W5500_MISO  12  // MISO pin
#define W5500_MOSI  11  // MOSI pin
#define W5500_SCK   13  // Clock pin

// MAC address (can be arbitrary or set according to network requirements)
byte mac[] = { 0xDE, 0xAD, 0xBE, 0xEF, 0xFE, 0xED };

EthernetClient client;

// Static IP settings
bool useDhcp = false;
IPAddress ip(192, 168, 1, 177);
IPAddress gateway(192, 168, 1, 1);
IPAddress subnet(255, 255, 255, 0);
IPAddress dns(192, 168, 1, 1);

/*
* SD Card
*/

#define SD_MISO_PIN 5
#define SD_MOSI_PIN 6
#define SD_SCLK_PIN 7
#define SD_CS_PIN   4

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

void parseIPAddress(const char* str, IPAddress& addr) {
  uint8_t parts[4] = {0};
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
            char c  = file.read();
            if (c == '\n' || c == '\r') {
                break;
            }
            line[i++] = c;
        }
        line[i] = '\0';

        if (i == 0) {
            continue;
        }

        char* eq = strchr(line, '=');
        if (!eq) {
            continue;
        }

        *eq = '\0';

        char* key = line;
        char* value = eq + 1;

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

void setup()
{
    // Start serial and wait
    Serial.begin(115200);  // Start serial communication
    while (!Serial) {
        ; // Wait for the serial port to be ready
    }

    Serial.println("Begin SETUP");

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
    SPI.begin(W5500_SCK, W5500_MISO, W5500_MOSI, W5500_CS);

    // Optional: Reset the W5500 module
    pinMode(W5500_RST, OUTPUT);
    digitalWrite(W5500_RST, LOW);
    delay(100);
    digitalWrite(W5500_RST, HIGH);
    delay(100);

    // Initialize Ethernet using DHCP to obtain an IP address
    Ethernet.init(W5500_CS);
    if (!useDhcp || Ethernet.begin(mac) == 0) {
        Serial.println("Using static IP...");
        // Initialize with static IP
        Ethernet.begin(mac, ip, dns, gateway, subnet);
    }

    // Print the assigned IP address
    Serial.print("IP: ");
    Serial.println(Ethernet.localIP());
    Serial.print("Gateway: ");
    Serial.println(Ethernet.gatewayIP());
    Serial.print("Subnet: ");
    Serial.println(Ethernet.subnetMask());
    Serial.print("DNS: ");
    Serial.println(Ethernet.dnsServerIP());

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
}

void loop()
{
}
