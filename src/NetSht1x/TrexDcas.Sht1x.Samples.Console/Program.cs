using System;
using System.Threading;

namespace TrexDcas.Sht1x.Samples.Console
{
	class Program
	{
		static void Main(string[] args)
		{
			IGpio gpio = new GpioCore();
			Sht1X sht = new Sht1X(gpio, 38, 40, crcCheck:true);

			System.Console.CancelKeyPress += (object sender, ConsoleCancelEventArgs eventArgs) =>
			{
				gpio.Dispose();
				sht.Dispose();
			};

			while (true)
			{
				sht.ReadTemperature();
				sht.ReadHumidity();
				sht.CalculateDewPoint();

				System.Console.WriteLine($"{sht}");
				//Sleep for 2 seconds
				Thread.Sleep(2000);
			}
		}
	}
}
