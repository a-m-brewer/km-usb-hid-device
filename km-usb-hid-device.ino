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
        delay(500);
        return;
    }

    cardSize = SD.cardSize() / (1024 * 1024);
    Serial.printf("SDCard Size: %d MB\n", cardSize);

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
    if (Ethernet.begin(mac) == 0) {
        Serial.println("DHCP failed, falling back to static IP...");

        // Static IP fallback settings
        IPAddress ip(192, 168, 1, 177);
        IPAddress gateway(192, 168, 1, 1);
        IPAddress subnet(255, 255, 255, 0);
        IPAddress dns(192, 168, 1, 1);

        // Initialize with static IP
        Ethernet.begin(mac, ip, dns, gateway, subnet);
    }

    // Print the assigned IP address
    Serial.print("IP Address: ");
    Serial.println(Ethernet.localIP());

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
