# net-sht1x #

The net-sht1x package is a .NET Standard 2 library used to communicate and control the Sensirion SHT1x series of temperature and humidity sensors. It was designed to be used primarily with the Raspberry Pi and depends on the [System.Device.Gpio](https://github.com/dotnet/iot) library.

SHT1x (including SHT10, SHT11 and SHT15) is Sensirion’s family of surface mountable relative humidity and temperature sensors. The sensors integrate sensor elements plus signal processing on a tiny foot print and provide a fully calibrated digital output. A unique capacitive sensor element is used for measuring relative humidity while temperature is measured by a band-gap sensor.

The package was tested using the Raspberry Pi B+ and Raspberry Pi 2. There shouldn't be issues running this on the older models, but no guarantees. If you do run into any problems, please let us know or create an [issue](https://github.com/TrexDcas/net-sht1x/issues) on the GitHub project page:

	https://github.com/TrexDcas/net-sht1x

The data sheet for the SHT1x series of sensors can be found here:

	http://bit.ly/1Pafs6j

This library provides the following functionality:

- Taking temperature measurements
- Taking humidity measurements
- Make dew point calculations
- Change the supplied voltage (5V, 4V, 3.5V, 3V, 2.5V)
- Enable or disable CRC checking
- Reading the Status Register
- Writing to the Status Register, provides the following functionality:
    - Turn `otpNoReload` on (will save about 10ms per measurement)
    - Turn on the internal heater element (for functionality analysis, refer to the data sheet list above for more information)
    - Change the resolution of measurements, High (14-bit temperature and 12-bit humidity) or Low (12-bit temperature and 8-bit humidity)

## Installation ##
Installation is pretty simple:

	NuGet (https://www.nuget.org/packages/TrexDcas.Sht1x)

## Usage ##
When instantiating a SHT1x object, the following default values are used if not specified:

	gpio_mode:		GPIO.BOARD
	vdd:			3.5V
	resolution:		High (14-bit temperature & 12-bit humidity)
	heater:			False
	otpNoReload:	False
	crcCheck:		True



## Credits ##

net-sht1x package is port of awesome Python package [pi-sht1x](https://github.com/drohm/pi-sht1x).

Thank you to all [pi-sht1x](https://github.com/drohm/pi-sht1x) contributors!
