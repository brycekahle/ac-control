//INSTANT C# NOTE: Formerly VB project-level imports:
using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Linq;
using System.Xml.Linq;

using System.IO;
using System.Text;


namespace CustomUSBeeSuiteDecoder
{
	public class CustomUSBeeSuiteDecoder
	{

		[System.Runtime.InteropServices.DllImport("usbeeste.dll", EntryPoint="?LoggedData@@YGJK@Z", ExactSpelling=true, CharSet=System.Runtime.InteropServices.CharSet.Ansi, SetLastError=true)]
		public static extern uint SampleData(int Index);
		// The SampleData routine returns a 4 byte value that contains a single sample of all the signals
		// The format of the 32 bits is as follows:
		//
		// MSB                          LSB
		// XXXXXXXXYYYYYYYYFEDCBA9876543210
		//
		// where XXXXXXXX is Channel 2 Analog value (0=-10V, 255 = +10V)
		//       YYYYYYYY is Channel 1 Analog value (0=-10V, 255 = +10V)
		//       F is logic level (0 or 1) for channel F
		//       E is logic level (0 or 1) for channel E
		//       D is logic level (0 or 1) for channel D
		//       ...
		//       0 is logic level (0 or 1) for channel 0

		private uint GTriggerSample;
		private uint GX1Sample;
		private uint GX2Sample;

		public void SetCaptureParameters(uint TriggerSample, uint X1Sample, uint X2Sample)
		{
			// This routine is called right at the end of a capture to pass the positions of the Trigger and X1 and X2 Cursors to the
			// custom decoding process.
			GTriggerSample = TriggerSample;
			GX1Sample = X1Sample;
			GX2Sample = X2Sample;

		}


		public void DecodeCustom(string OutFilename, int NumberOfSamples, byte RateIndex, string Parameters)
		{
			uint OldSample = 0;
            FileStream FS = null;
			try
			{

				// This is a custom bus decoder Processing Routine
				// 
				// The passed in variables are as follows:
				// OutFilename     - the file that all of the decoded Entries get written to.  This is the file that the USBee Suite
				//                   will read to display the data on the waveline.
				// NumberOfSamples - How many samples are in the sample buffer
				// RateIndex       - An index of the sample rate that the samples were taken.  
				//                   17=1Msps,27=2Msps,37=3Msps,47=4Msps,67=6Msps,87=8Msps,127=12Msps,167=16Msps,247=24Msps
				// Parameters      - User defined string passed from the USBee Suite user interface Channel Setting for the custom decoder.
				//                   Use this string to pass in any parameters that your decoder needs to know, such as what channels to use 
				//                   in decoding, which protocol if you have multiple protocols supported here, and how you want the data formatted.

				// Below is an example set of Custom Protocol decoders that show how to access the sample buffer and how to generate output that get sent to the screen.

				// Setup the File Stream that stores the Output Entry Information
				FS = new FileStream(OutFilename, FileMode.Append, FileAccess.Write);
				BinaryWriter BW = new BinaryWriter(FS, Encoding.ASCII);
				double SamplingRate = 0;

				// Determining the actual sample rate so it can be written in the OutputFile
				//                   17=1Msps,27=2Msps,37=3Msps,47=4Msps,67=6Msps,87=8Msps,127=12Msps,167=16Msps,247=24Msps
				if (RateIndex == 17)
				{
					SamplingRate = 1000000;
				}
				else if (RateIndex == 27)
				{
					SamplingRate = 2000000;
				}
				else if (RateIndex == 37)
				{
					SamplingRate = 3000000;
				}
				else if (RateIndex == 47)
				{
					SamplingRate = 4000000;
				}
				else if (RateIndex == 67)
				{
					SamplingRate = 6000000;
				}
				else if (RateIndex == 87)
				{
					SamplingRate = 8000000;
				}
				else if (RateIndex == 127)
				{
					SamplingRate = 12000000;
				}
				else if (RateIndex == 167)
				{
					SamplingRate = 16000000;
				}
				else if (RateIndex == 247)
				{
					SamplingRate = 24000000;
				}

				if (Convert.ToBoolean(Parameters.ToUpper().IndexOf("NECIR") + 1))
				{
					// Sample Decoder that just detects when a signal changes state
					// The signal to use for the detection is specified in the Parameters as the second parameter
					var tempVar7 = "NEC IR Decoder 3.0";
					WriteEntry(ref BW, Convert.ToUInt32(0), Convert.ToUInt32(100), ref tempVar7);


					var Params = Parameters.Split(' ', ',', '-');
                    int SignalMask = 1 << Convert.ToInt32(Params[1]); //Make the mask that will mask off the channel we want in the sample

					const int LOOKING_FOR_HEADER = 1;
					const int LOOKING_FOR_BITS = 2;
					int DecodeState = LOOKING_FOR_HEADER; // Holds what state of the decoder we are in

					int ByteAccumlator = 0; // Holds the accumulated bits for each byte
					int BitCounter = 0; // Holds how many bits we have accumulated in this byte so far
					int ByteStartSample = 0; // Holds the sample at the start of the byte

					uint Data = 0; // Holds the state of the signal at the current sample
					int tEdge1 = 0; // Where the first edge is
					int tEdge2 = 0; // Where the second edge is
					double tPulseWidth = 0; // The pulsewidth in seconds

					// Now go from the start of the samples to the end and process the signal
					for (int Sample = 0; Sample < NumberOfSamples; Sample++)
					{

						Data = Convert.ToUInt32(SampleData(Sample) & SignalMask);

						if (DecodeState == LOOKING_FOR_HEADER)
						{

							if (Data != 0)
							{
								// We found a High which starts the Header
								// Now look for the next edge

								tEdge1 = WhereIsTheNextEdge(Sample, SignalMask, NumberOfSamples);

								if (tEdge1 >= 0)
								{

									// Check to see if this rising edge is in the right timeframe 
									tPulseWidth = (tEdge1 - Sample) / SamplingRate;
                                    
									if ((tPulseWidth >= 0.008) && (tPulseWidth <= 0.01))
									{

										// Now look for the falling edge

										tEdge2 = WhereIsTheNextEdge(tEdge1, SignalMask, NumberOfSamples);

										if (tEdge2 >= 0)
										{
											// Check to see if this rising edge is in the right timeframe 
											tPulseWidth = (tEdge2 - tEdge1) / SamplingRate;
											if ((tPulseWidth >= 0.004) && (tPulseWidth <= 0.005))
											{
												// Great!  Valid Header Format!  Look for bits from this point on

												// Write out a Header Marker (remove this if you don't need the header)
												var tempVar8 = "Lead Code";
												WriteEntry(ref BW, Convert.ToUInt32(Sample), Convert.ToUInt32(tEdge2), ref tempVar8);

												DecodeState = LOOKING_FOR_BITS;
												Sample = tEdge2;

												// Initialize the Byte Accumulation variables
												ByteStartSample = Sample;
												ByteAccumlator = 0;
												BitCounter = 0;

												continue;
											}
											else
											{
												// Pulse is not the right size so bail and keep looking
												Sample = tEdge2;
												continue;
											}
										}
										else
										{
											// No edges at all!  So we are done
											break;
										}

									}
									else
									{
										// Pulse is not the right size so bail and keep looking
										Sample = tEdge1;
										continue;
									}
								}
								else
								{
									// No edges at all!  So we are done
									break;
								}

							}

						}
						else if (DecodeState == LOOKING_FOR_BITS)
						{

							if (BitCounter == 4)
							{
								// We have an entire byte worth of data so output the information
								var tempVar10 = Convert.ToString(ByteAccumlator, 16).ToUpper();
								WriteEntry(ref BW, Convert.ToUInt32(ByteStartSample), Convert.ToUInt32(tEdge2), ref tempVar10);
								BitCounter = 0;
								ByteAccumlator = 0;
							}

							if (Data != 0)
							{
								// We found a High which starts the bit
								// Now look for the next edge

								tEdge1 = WhereIsTheNextEdge(Sample, SignalMask, NumberOfSamples);

								if (tEdge1 >= 0)
								{

									// Check to see if this falling edge is in the right timeframe 
									tPulseWidth = (tEdge1 - Sample) / SamplingRate;

									if ((tPulseWidth >= 0.0005) && (tPulseWidth <= 0.0007))
									{
										// Good start of a bit
										// Now look for the rising edge

										tEdge2 = WhereIsTheNextEdge(tEdge1, SignalMask, NumberOfSamples);

										if (tEdge2 >= 0)
										{
											// Check to see if this rising edge is in the right timeframe for a logic "0"
											tPulseWidth = (tEdge2 - tEdge1) / SamplingRate;
											if ((tPulseWidth >= 0.0004) && (tPulseWidth <= 0.0006))
											{
												// Great!  Valid 0 Bit Format!  

												// Write out a Bit Marker (remove this if you don't need the bit)
												//WriteEntry(BW, CUInt(Sample), CUInt(tEdge2), "0")

												// Add this bit to the accumulators (MSB first)
												ByteAccumlator = ByteAccumlator << 1; // Shift the Accumulator
												ByteAccumlator &= 0x0E; // Clear out the LSBit

												// Mark the start of the byte if so
												if (BitCounter == 0)
												{
													ByteStartSample = Sample;
												}

												// Next Bit next time
												BitCounter = BitCounter + 1;

												Sample = tEdge2;
												continue;
											}
											else if ((tPulseWidth >= 0.001) && (tPulseWidth <= 0.002))
											{
												// Great!  Valid 1 Bit Format!  

												// Write out a Bit Marker (remove this if you don't need the bit)
												//WriteEntry(BW, CUInt(Sample), CUInt(tEdge2), "1")

												// Add this bit to the accumulators (MSB first)
												ByteAccumlator = ByteAccumlator << 1; // Shift the Accumulator
												ByteAccumlator |= 0x01; // Set the LSBit

												// Mark the start of the byte if so
												if (BitCounter == 0)
												{
													ByteStartSample = Sample;
												}

												// Next Bit next time
												BitCounter = BitCounter + 1;

												Sample = tEdge2;
												continue;

											}
											else
											{
												// Pulse is not the right size so bail and keep looking
												DecodeState = LOOKING_FOR_HEADER;
												Sample = tEdge2;
												continue;
											}
										}
										else
										{
											// No edges at all!  So we are done
											break;
										}

									}
									else
									{
										// Pulse is not the right size so bail and keep looking
										Sample = tEdge1;
										DecodeState = LOOKING_FOR_HEADER;
										continue;
									}
								}
								else
								{
									// No edges at all!  So we are done
									break;
								}

							}

						}
					}

				}

				// Close the Output File
				

			}
            finally
            {
                if (FS != null) FS.Close();
            }

		}

		public int WhereIsTheNextEdge(int tSample, int tSignalMask, int NumberOfSamples)
		{
			// This function finds where the next edge is starting at Sample tSample

			uint OldData = Convert.ToUInt32(SampleData(tSample) & tSignalMask);

			for (int Sample = tSample; Sample < NumberOfSamples; Sample++)
			{

				uint Data = Convert.ToUInt32(SampleData(Sample) & tSignalMask);

				if (Data != OldData)
				{
					return Sample;
				}

			}

			return -1;

		}

		public void WriteEntry(ref BinaryWriter BW, UInt32 StartSample, UInt32 EndSample, ref string TextString)
		{

			// DO NOT CHANGE THIS ROUTINE!!!
			// This routine writes the Entry in the file format that is used by the Custom Decoder
			// This entry specifies the Start Sample, End Sample and the text string to display
            //try
            //{

				BW.Write(StartSample);
				BW.Write(EndSample);

				// Write the length of the string in bytes (include the 0 at the end in the count)
				UInt32 tStrLen = 0;
				tStrLen = Convert.ToUInt32(TextString.Length + 1);
				BW.Write(tStrLen);

				// Now write out the characters one byte at a time and put a 0 at the end
                BW.Write(TextString.ToCharArray());
				BW.Write((byte)0);

            //}
            //catch (Exception ex)
            //{

            //}


		}

	}


}