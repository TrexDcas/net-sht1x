using System;
using System.Device.Gpio;
namespace TrexDcas.Sht1x
{
	public class GpioCore:IGpio
	{
		private readonly GpioController _controller;

		public GpioCore(PinNumberingScheme numberingScheme=PinNumberingScheme.Board)
		{
			_controller = new GpioController(numberingScheme);
		}

		public void Setup(int pin, int mode)
		{
			if(!_controller.IsPinOpen(pin))
				return;

			PinMode pinMode = PinMode.Input;
			switch (mode)
			{
				case GPIO.IN: pinMode = PinMode.Input;
					break;
				case GPIO.OUT: pinMode = PinMode.Output;
					break;
				default:
					throw new NotSupportedException($"Unsupported pin mode{mode}");
			}
			if(!_controller.IsPinOpen(pin))
				_controller.OpenPin(pin,pinMode);

			_controller.SetPinMode(pin,pinMode);
		}

		public void Output(int pin, int value)
		{
			if(!_controller.IsPinOpen(pin)) _controller.OpenPin(pin,PinMode.Output);
			_controller.Write(pin,value);
		}

		public int Input(int pin)
		{
			if(!_controller.IsPinOpen(pin)) _controller.OpenPin(pin,PinMode.Input);
			var result= _controller.Read(pin);

			return (int)result;
		}

		public void Cleanup()
		{

		}

		public void Dispose()
		{
			_controller.Dispose();
		}
	}
}
