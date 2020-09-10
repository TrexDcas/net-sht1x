using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using Serilog;

namespace TrexDcas.Sht1x
{
	public class Sht1XException : Exception
	{
		public Sht1XException()
		{
		}

		public Sht1XException(string message) : base(message)
		{
		}
	}

	public class Sht1X : IDisposable
	{
		private readonly IReadOnlyDictionary<int, string> GPIO_FUNCS = new Dictionary<int, string>
		{
			{-1, "GPIO.UNKNOWN"},
			{0, "GPIO.OUT"},
			{1, "GPIO.IN"},
			{10, "GPIO.BOARD"},
			{11, "GPIO.BCM"},
			{40, "GPIO.SERIAL"},
			{41, "GPIO.SPI"},
			{42, "GPIO.I2C"},
			{43, "GPIO.HARD_PWM"}
		};

		private class COF
		{
			public static IReadOnlyDictionary<double, double> D1_VDD_C = new Dictionary<double, double>
			{
				{5, -40.1}, {4, -39.8}, {3.5, -39.7}, {3, -39.6}, {2.5, -39.4}
			};

			public static IReadOnlyDictionary<double, double> D1_VDD_F = new Dictionary<double, double>
			{
				{5, -40.2}, {4, -39.6}, {3.5, -39.5}, {3, -39.3}, {2.5, -38.9}
			};


			public static IReadOnlyDictionary<double, double> D2_SO_C = new Dictionary<double, double>
			{
				{14, 0.01}, {12, 0.04}
			};

			public static IReadOnlyDictionary<double, double> D2_SO_F = new Dictionary<double, double>
			{
				{14, 0.018}, {12, 0.072}
			};

			public static IReadOnlyDictionary<double, double> C1_SO = new Dictionary<double, double>
			{
				{12, -2.0468}, {8, -2.0468}
			};


			public static IReadOnlyDictionary<double, double> C2_SO = new Dictionary<double, double>
			{
				{12, 0.0367}, {8, 0.5872}
			};

			public static IReadOnlyDictionary<double, double> C3_SO = new Dictionary<double, double>
			{
				{12, -0.0000015955}, {8, -0.00040845}
			};

			public static IReadOnlyDictionary<double, double> T1_SO = new Dictionary<double, double>
			{
				{12, 0.01}, {8, 0.01}
			};

			public static IReadOnlyDictionary<double, double> T2_SO = new Dictionary<double, double>
			{
				{12, 0.00008}, {8, 0.00128}
			};
		}

		private readonly int[] CRC = new int[]
		{
			0, 49, 98, 83, 196, 245, 166, 151, 185, 136, 219, 234, 125, 76, 31, 46, 67, 114, 33, 16, 135,
			182, 229, 212, 250, 203, 152, 169, 62, 15, 92, 109, 134, 183, 228, 213, 66, 115, 32, 17, 63,
			14, 93, 108, 251, 202, 153, 168, 197, 244, 167, 150, 1, 48, 99, 82, 124, 77, 30, 47, 184, 137,
			218, 235, 61, 12, 95, 110, 249, 200, 155, 170, 132, 181, 230, 215, 64, 113, 34, 19, 126, 79,
			28, 45, 186, 139, 216, 233, 199, 246, 165, 148, 3, 50, 97, 80, 187, 138, 217, 232, 127, 78, 29,
			44, 2, 51, 96, 81, 198, 247, 164, 149, 248, 201, 154, 171, 60, 13, 94, 111, 65, 112, 35, 18,
			133, 180, 231, 214, 122, 75, 24, 41, 190, 143, 220, 237, 195, 242, 161, 144, 7, 54, 101, 84,
			57, 8, 91, 106, 253, 204, 159, 174, 128, 177, 226, 211, 68, 117, 38, 23, 252, 205, 158, 175,
			56, 9, 90, 107, 69, 116, 39, 22, 129, 176, 227, 210, 191, 142, 221, 236, 123, 74, 25, 40, 6,
			55, 100, 85, 194, 243, 160, 145, 71, 118, 37, 20, 131, 178, 225, 208, 254, 207, 156, 173, 58,
			11, 88, 105, 4, 53, 102, 87, 192, 241, 162, 147, 189, 140, 223, 238, 121, 72, 27, 42, 193, 240,
			163, 146, 5, 52, 103, 86, 120, 73, 26, 43, 188, 141, 222, 239, 130, 179, 224, 209, 70, 119, 36,
			21, 59, 10, 89, 104, 255, 206, 157, 172
		};

		private readonly IReadOnlyDictionary<string, int> Commands = new Dictionary<string, int>
		{
			{"Temperature", 0b00000011},
			{"Humidity", 0b00000101},
			{"ReadStatusRegister", 0b00000111},
			{"WriteStatusRegister", 0b00000110},
			{"SoftReset", 0b00011110},
			{"NoOp", 0b00000000}
		};

		private readonly IReadOnlyDictionary<string, int[]> RESOLUTION = new Dictionary<string, int[]>
		{
			{"HIGH", new int[2] {14, 12}},
			{"LOW", new int[2] {12, 8}}
		};

		private readonly IReadOnlyDictionary<string, double> VDD = new Dictionary<string, double>
		{
			{"5V", 5}, {"4V", 4}, {"3.5V", 3.5}, {"3V", 3}, {"2.5V", 2.5}
		};


		private int DataPin { get; set; }
		private int SckPin { get; set; }
		private double Vdd { get; set; }

		private int[] _resolution;
		private int[] Resolution
		{
			get { return _resolution;}
			set
			{
				_resolution = value;
				InitializeSensor();
			}
		}

		private bool _heater = false;

		private bool Heater
		{
			get { return _heater; }
			set
			{
				_heater = value;
				InitializeSensor();
			}
		}

		private bool _otpNoReload = false;

		private bool OtpNoReload
		{
			get { return _otpNoReload; }
			set
			{
				_otpNoReload = value;
				InitializeSensor();
			}
		}

		private bool CrcCheck { get; set; }
		private int Command { get; set; }
		private int StatusRegister { get; set; }
		public double? TemperatureC { get; private set; }
		public double? TemperatureF { get; private set; }
		public double? Humidity { get; private set; }
		public double? DewPoint { get; private set; }

		private readonly IGpio _gpio;

		public Sht1X(IGpio gpio,
			int dataPin, int sckPin,
			string vdd = "3.5V", string resolution = "High",
			bool heater = false, bool otpNoReload = false, bool crcCheck = true)
		{
			_gpio = gpio;
			this.DataPin = dataPin;
			this.SckPin = sckPin;
			this.Vdd = VDD.ContainsKey(vdd.ToUpper()) ? VDD[vdd.ToUpper()] : this.VDD["3.5V"];
			this._resolution = RESOLUTION.ContainsKey(resolution.ToUpper())
				? RESOLUTION[resolution.ToUpper()]
				: this.RESOLUTION["HIGH"];
			this._heater = heater;
			this._otpNoReload = otpNoReload;
			this.CrcCheck = crcCheck;
			this.Command = this.Commands["NoOp"];
			this.StatusRegister = 0b0000000;
			this.TemperatureC = null;
			this.TemperatureF = null;
			this.Humidity = null;
			this.DewPoint = null;
			this.InitializeSensor();

			Log.Information($"Initial configuration:\nData Pin: {this.DataPin}\nClock Pin: {this.SckPin}\n"
			                + $"Vdd: {this.Vdd}\nResolution: {this.Resolution}\n"
			                + $"Heater: {this.Heater}\nOTP no reload: {this.OtpNoReload}\nCRC check: {this.CrcCheck}");
		}

		private string ToBinaryString(int data, int padLeft= 8)
		{
			var result = Convert.ToString(data, 2).PadLeft(padLeft, '0');
			return result.Substring(0, padLeft);
		}

		/// <summary>
		/// Resets the connection to the sensor and then initializes the SHT1x's status register based on the values of the object.
		/// Heater: default is 0
		/// No reload from OTP: default is 0
		/// Resolution: default is 0
		/// The status register mask is built based on the object attributes.
		/// </summary>
		private void InitializeSensor()
		{
			this.ResetConnection();
			int mask = 0;
			if (this.Heater)
				mask += 4;

			if (this.OtpNoReload)
				mask += 2;

			if (this.Resolution[0] == this.RESOLUTION["LOW"][0])
				mask += 1;

			Log.Information($"Initializing sensor using bit mask: {ToBinaryString(mask)}");
			this.WriteStatusRegister(mask);
		}

		/// <summary>
		/// Sends command to the SHT1x sensor to read the temperature. Values for both celsius and fahrenheit are calculated.
		/// </summary>
		public double? ReadTemperature()
		{
			this.Command = this.Commands["Temperature"];
			this.SendCommand();
			var raw = this.ReadMesurement();
			this.TemperatureC = Math.Round(raw * COF.D2_SO_C[this.Resolution[0]] + COF.D1_VDD_C[this.Vdd], 2);
			this.TemperatureF = Math.Round(raw * COF.D2_SO_F[this.Resolution[0]] + COF.D1_VDD_F[this.Vdd], 2);

			Log.Information($"Temperature: {this.TemperatureC}*C [{this.TemperatureF}*F]");
			return TemperatureC;
		}

		/// <summary>
		/// Sends command to the SHT1x sensor to read the temperature compensated humidity. If the read_temperature
		/// function has not been called previously and the temperature parameter is not used, it will read the
		/// 	temperature from the sensor.
		/// :param temperature: Optional, temperature, in celsius, used to compensate when temperatures are significantly
		/// 	different from 25C (~77F) when calculating relative humidity.
		/// </summary>
		/// <param name="temperature"></param>
		/// <returns></returns>
		public double? ReadHumidity(double? temperature = null)
		{
			if (temperature == null)
			{
				if (this.TemperatureC == null)
					this.ReadTemperature();
				temperature = this.TemperatureC;
			}

			this.Command = this.Commands["Humidity"];
			this.SendCommand();
			var raw = this.ReadMesurement();

			var linearHumidity = COF.C1_SO[this.Resolution[1]] + (COF.C2_SO[this.Resolution[1]] * raw) + (COF.C3_SO[this.Resolution[1]] * Math.Pow(raw, 2.0));

			this.Humidity =
				Math.Round((temperature.Value - 25) * (COF.T1_SO[this.Resolution[1]] + COF.T2_SO[this.Resolution[1]] * raw) + linearHumidity, 2);

			Log.Information($"Relative Humidity: {this.Humidity}%");
			return this.Humidity;
		}

		/// <summary>
		/// Calculates the dew point, based on the given temperature and humidity. If the temperature or humidity are not
		/// given it will read in the values from the sensor.
		/// </summary>
		/// <param name="temperature"></param>
		/// <param name="humidity"></param>
		/// <returns>Dew point</returns>
		public double? CalculateDewPoint(double? temperature = null, double? humidity = null)
		{
			if (temperature == null)
			{
				if (this.TemperatureC == null)
					this.ReadTemperature();
				temperature = this.TemperatureC;
			}

			if (humidity == null)
			{
				if (this.Humidity == null)
					this.ReadHumidity();
				humidity = this.Humidity;
			}

			var tn = 243.12;
			var m = 17.62;

			if (TemperatureC <= 0)
			{
				tn = 272.62;
				m = 22.46;
			}

			var log_humidity = Math.Log(humidity.Value / 100.0);
			var ew = (m * temperature.Value) / (tn + temperature.Value);
			this.DewPoint = Math.Round(tn * (log_humidity + ew) / m - (log_humidity + ew), 2);

			Log.Information($"Dew Point: {this.DewPoint}*C");
			return this.DewPoint;
		}

		/// <summary>
		/// Sends the given command to the SHT1x sensor and verifies acknowledgement. If the command is for
		/// taking a measurement it will also ensure that the measurement is taking place and waits for the
		/// measurement to complete.
		/// </summary>
		/// <param name="measurement"></param>
		private void SendCommand(bool measurement = true)
		{
			var command = Commands.SingleOrDefault(kvp => kvp.Value == this.Command);

			if (string.IsNullOrWhiteSpace(command.Key))
			{
				var message = $"The command was not found: {this.Command}";
				var ex = new Sht1XException(message);
				Log.Error(ex, "Can not send command");
				throw ex;
			}

			this.TransmissionStart();
			this.SendByte(this.Command);
			this.GetAck(command.Key);

			if (measurement)
			{
				var ack = _gpio.Input(this.DataPin);
				Log.Information("SHT1x is taking measurement.");
				if (ack == GPIO.LOW)
				{
					var message = $"SHT1x is not in the proper measurement state: DATA line is LOW.";
					var ex = new Sht1XException(message);
					Log.Error(ex, "Can not send command");
					throw ex;
				}

				this.WaitForResult();
			}
		}

		/// <summary>
		/// Waits for the sensor to complete measurement. The time to complete depends
		/// on the number of bits used for measurement:
		/// 8-bit:  20ms
		/// 12-bit: 80ms
		/// 14-bit: 320ms
		/// 	Raises an exception if the Data Ready signal hasn't been received after 350 milliseconds.
		/// </summary>
		private void WaitForResult()
		{
			_gpio.Setup(this.DataPin, GPIO.IN);
			var dataReady = GPIO.HIGH;

			for (var i = 0; i < 35; i++)
			{
				Thread.Sleep(10);
				dataReady = _gpio.Input(this.DataPin);
				if (dataReady == GPIO.LOW)
				{
					Log.Information("Measurement complete.");
					break;
				}
			}

			if (dataReady == GPIO.HIGH)
			{
				var ex = new Sht1XException("Sensor has not completed measurement after max time allotment");
				Log.Error(ex, "Sensor measurement not completed.");
				throw ex;
			}
		}

		/// <summary>
		/// Reads the measurement data from the SHT1x sensor. If crc_check is set to True the CRC value
		/// will be read and verified, otherwise the transmission will end.
		/// </summary>
		/// <returns>16-bit value (short)</returns>
		private int ReadMesurement()
		{
			// Get the MSB
			var value = this.GetByte();
			value <<= 8;
			Log.Information($"Reading measurement MSB : {ToBinaryString(value)}");
			this.SendAck();
			//Get the LSB
			value |= this.GetByte();
			Log.Information($"Reading measurement LSB : {ToBinaryString(value)}");

			if (this.CrcCheck)
				this.ValidateCrc(value);
			else
				this.TransmissionEnd();
			return value;
		}

		/// <summary>
		/// Reads a single byte from the SHT1x sensor.
		/// </summary>
		/// <returns>8-bit value (byte)</returns>
		private int GetByte()
		{
			_gpio.Setup(this.DataPin, GPIO.IN);
			_gpio.Setup(this.SckPin, GPIO.OUT);

			var data = 0b00000000;
			for (var i = 0; i < 8; i++)
			{
				this.TogglePin(this.SckPin, GPIO.HIGH);
				data |= ( _gpio.Input(this.DataPin) << (7 - i) ) ;
				this.TogglePin(this.SckPin, GPIO.LOW);
			}

			return data;
		}

		/// <summary>
		///  Sends a single byte to the SHT1x sensor
		/// </summary>
		/// <param name="data"></param>
		private void SendByte(int data)
		{
			_gpio.Setup(this.DataPin, GPIO.OUT);
			_gpio.Setup(this.SckPin, GPIO.OUT);

			for (var i = 0; i < 8; i++)
			{
				this.TogglePin(this.DataPin, data & (1 << (7 - i)));
				this.TogglePin(this.SckPin, GPIO.HIGH);
				this.TogglePin(this.SckPin, GPIO.LOW);
			}
		}


		/// <summary>
		/// Toggles the state of the specified pin. If the specified pin is the SCK pin, it will Noop after setting its new state.
		/// </summary>
		/// <param name="pin">Pin to toggle state</param>
		/// <param name="state">State to change the pin, GPIO.LOW or GPIO.HIGH.</param>
		private void TogglePin(int pin, int state)
		{
			_gpio.Output(pin, state);
			if (pin == SckPin)
			{
				var sw = new Stopwatch();
				// Ticks per second
				var freq = Stopwatch.Frequency;
				var ticks100ns = freq * 0.0000001;

				sw.Start();
				while (sw.ElapsedTicks < ticks100ns) ;
				sw.Stop();
				Thread.Sleep(1);
			}
		}


		/// <summary>
		/// Sends the transmission start sequence to the sensor to initiate communication.
		/// </summary>
		private void TransmissionStart()
		{
			_gpio.Setup(this.DataPin, GPIO.OUT);
			_gpio.Setup(this.SckPin, GPIO.OUT);

			this.TogglePin(this.DataPin, GPIO.HIGH);
			this.TogglePin(this.SckPin, GPIO.HIGH);

			this.TogglePin(this.DataPin, GPIO.LOW);
			this.TogglePin(this.SckPin, GPIO.LOW);

			this.TogglePin(this.SckPin, GPIO.HIGH);
			this.TogglePin(this.DataPin, GPIO.HIGH);

			this.TogglePin(this.SckPin, GPIO.LOW);
		}

		private void TransmissionEnd()
		{
			_gpio.Setup(this.DataPin, GPIO.OUT);
			_gpio.Setup(this.SckPin, GPIO.OUT);

			this.TogglePin(this.DataPin, GPIO.HIGH);
			this.TogglePin(this.SckPin, GPIO.HIGH);

			this.TogglePin(this.SckPin, GPIO.LOW);
		}

		/// <summary>
		/// Gets ACK from the SHT1x confirming data was received by the sensor.
		/// Command issued to the sensor.
		/// </summary>
		/// <param name="commandName"></param>
		private void GetAck(string commandName)
		{
			_gpio.Setup(DataPin, GPIO.IN);
			_gpio.Setup(SckPin, GPIO.OUT);

			this.TogglePin(SckPin, GPIO.HIGH);

			var ack = _gpio.Input(DataPin);
			Log.Information($"Command {commandName} [{ToBinaryString(Command)}] acknowledged: {ack}");

			if (ack == GPIO.HIGH)
			{
				var message =
					$"SHT1x failed to properly receive command {commandName} [{ToBinaryString(Command)}]";
				var ex = new Sht1XException(message);
				Log.Error(ex, "Can not get ACK.");
				throw ex;
			}

			this.TogglePin(this.SckPin, GPIO.LOW);
		}

		/// <summary>
		/// Sends ACK to the SHT1x confirming byte measurement data was received by the caller.
		/// </summary>
		private void SendAck()
		{
			_gpio.Setup(this.DataPin, GPIO.OUT);
			_gpio.Setup(this.SckPin, GPIO.OUT);

			this.TogglePin(this.DataPin, GPIO.HIGH);
			this.TogglePin(this.DataPin, GPIO.LOW);

			this.TogglePin(this.SckPin, GPIO.HIGH);
			this.TogglePin(this.SckPin, GPIO.LOW);
		}

		/// <summary>
		/// Retrieves the contents of the Status Register as a binary integer.
		/// </summary>
		/// <returns>Status register value</returns>
		private int ReadStatusRegister()
		{
			this.Command = this.Commands["ReadStatusRegister"];
			this.SendCommand(false);
			this.StatusRegister = this.GetByte();
			Log.Information($"Status Register read: {ToBinaryString(this.StatusRegister)}");

			if (this.CrcCheck)
				this.ValidateCrc(this.StatusRegister, false);
			else
				this.TransmissionEnd();

			Log.Information($"Read Status Register: {ToBinaryString(this.StatusRegister)}");
			return this.StatusRegister;
		}

		/// <summary>
		/// Writes the 8-bit value to the Status Register. Only bits 0-2 are R/W, bits 3-7 are read-only.
		/// bit 2 - Heater: defaults to off
		///0: heater off
		///1: heater on
		///bit 1 - no reload from OTP: defaults to off
		///0: reload on
		///1: reload off
		///bit 0 - measurement resolution: defaults to 0
		///0: 14bit Temp/12bit RH
		///1: 12bit Temp/8bit RH
		///Example value: 0b00000010
		///This value uses the highest measurement resolution (14bit Temp/12bit RH), no reload from OTP is enabled
		///	and the heater is off.
		/// </summary>
		/// <param name="mask">Binary integer used to write to the Status Register</param>
		private void WriteStatusRegister(int mask)
		{
			this.Command = this.Commands["WriteStatusRegister"];
			this.SendCommand(false);
			Log.Information($"Writing Status Register: {ToBinaryString(mask)}");

			this.SendByte(mask);
			this.GetAck("WriteStatusRegister");
			this.StatusRegister = mask;
		}


		private void ResetStatusRegister()
		{
			this.WriteStatusRegister(Commands["Noop"]);
		}

		private int ReverseByte(int data)
		{
			//return (data * 8623620610 & 1136090292240) % 1023;

			byte result = 0x00;

			for (byte mask = 0x80; Convert.ToInt32(mask) > 0; mask >>= 1)
			{
				// shift right current result
				result = (byte) (result >> 1);

				// tempbyte = 1 if there is a 1 in the current position
				var tempbyte = (byte)(data & mask);
				if (tempbyte != 0x00)
				{
					// Insert a 1 in the left
					result = (byte) (result | 0x80);
				}
			}
			return result;
		}


		/// <summary>
		/// Reverses the Status Register byte.
		/// </summary>
		private int ReverseStatusRegister()
		{
			var srReversed = this.ReverseByte(this.StatusRegister);
			var crcRegister = (srReversed >> 4) << 4;
			Log.Information($"Status register reversed: {ToBinaryString(crcRegister)}");

			return crcRegister;
		}

		/// <summary>
		/// Performs CRC validation using Byte-wise calculation.
		/// </summary>
		/// <param name="data">Data retrieved from the SHT1x sensor, either measurement data or from the Status Register.</param>
		/// <param name="measurement">Indicates if the data parameter is from a measurement or from reading the Status Register</param>
		/// <returns></returns>
		private bool ValidateCrc(int data, bool measurement = true)
		{
			this.SendAck();
			var crcValue = this.GetByte();
			this.TransmissionEnd();
			Log.Information($"CRC value from sensor: {ToBinaryString(crcValue)}");

			var crcStartValue = this.ReverseStatusRegister();
			Log.Information($"CRC start value: {ToBinaryString(crcStartValue)}");

			var crcLookup = CRC[crcStartValue ^ this.Command];
			Log.Information($"CRC command lookup value: {ToBinaryString(crcLookup)}");

			Log.Information($"Sensor data (MSB and LSB): {ToBinaryString(data, 16)}");
			int crcFinal = 0;
			if (measurement)
			{
				crcLookup = CRC[crcLookup ^ (data >> 8)];
				Log.Information($"CRC MSB lookup value: {ToBinaryString(crcLookup)}");

				crcFinal = CRC[crcLookup ^ (data & 0b0000000011111111)];
				Log.Information($"CRC LSB lookup value: {ToBinaryString(crcFinal)}");
			}
			else
			{
				crcFinal = CRC[crcLookup ^ data];
				Log.Information($"CRC data lookup value: {ToBinaryString(crcFinal)}");
			}


			var crcFinalReversed = this.ReverseByte(crcFinal);
			Log.Information($"CRC calculated value (reversed):{ToBinaryString(crcFinalReversed)}");

			if (crcValue != crcFinalReversed)
			{
				this.SoftReset();
				var message = "CRC error! Sensor has been reset, please try again.\n"
				              + $"CRC value from sensor: {ToBinaryString(crcValue)}\nCRC calculated value: {ToBinaryString(crcFinalReversed)}";

				var ex = new Sht1XException(message);
				Log.Error(ex, "CRC is not valid.");
				throw ex;
			}

			return true;
		}

		/// <summary>
		/// Resets the serial interface to the Sht1x sensor. The status register preserves its content.
		/// </summary>
		private void ResetConnection()
		{
			_gpio.Setup(this.DataPin, GPIO.OUT);
			_gpio.Setup(this.SckPin, GPIO.OUT);

			this.TogglePin(this.DataPin, GPIO.HIGH);
			for (var i = 0; i < 10; i++)
			{
				this.TogglePin(this.SckPin, GPIO.HIGH);
				this.TogglePin(this.SckPin, GPIO.LOW);
			}
		}

		private void SoftReset()
		{
			this.Command = this.Commands["SoftReset"];
			this.SendCommand(false);
			Thread.Sleep(15);
			this.StatusRegister = 0b00000000;
		}


		public override string ToString()
		{
			return
				$"Temperature: {TemperatureC}*C [{TemperatureF}*F]\nRelative Humidity: {Humidity}%\nDew Point: {DewPoint}*C\n";
		}

		public void Dispose()
		{
			_gpio.Cleanup();
		}
	}
}
