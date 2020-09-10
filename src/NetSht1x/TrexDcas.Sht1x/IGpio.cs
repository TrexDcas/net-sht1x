using System;

namespace TrexDcas.Sht1x
{

	public interface IGpio:IDisposable
	{
		void Setup(int pin, int mode);
		void Output(int pin, int value);

		int Input(int pin);

		void Cleanup();

	}
}
