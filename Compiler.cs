using System;
using System.Collections;
using System.IO;
using System.Reflection;
using mwgc.AseLib;
using mwgc.RawGeometry;
using mwgc.RealEngine;

namespace mwgc
{
	class Compiler
	{
		public static CompilerOptions Options = new CompilerOptions();

		static void PrintBanner()
		{
			Assembly asm = Assembly.GetExecutingAssembly();
			AssemblyName asmName = asm.GetName();
			
			Console.WriteLine("NFS:MW Geometry Compiler (mwgc) " + asmName.Version.ToString());
			Console.WriteLine("Copyright(C) 2005 - 2006, AruTec Inc. (Arushan), All Rights Reserved.");
			Console.WriteLine("Contact: oneforaru at gmail dot com (bug reports only)");
			Console.WriteLine();
			Console.WriteLine("Disclaimer: This program is provided as is without any warranties of any kind.");
			//Console.WriteLine("            All reverse-engineering performed to develop this software was done");
			//Console.WriteLine("            for the sole purpose of accomplishing interoperability.");
			Console.WriteLine();
		}

		static void PrintUsage()
		{
			Console.WriteLine("Usage:   mwrc <options> source.mwr [geometry.bin]");
			Console.WriteLine();
			Console.WriteLine("Options: -xname <name>     Set the X-Name for the model");
			Console.WriteLine("         -matlist          Provides a list of materials");
			Console.WriteLine("         -xlink            Provides a list of geometry crosslinks");
			//Console.WriteLine("         -protect          Protect the result geometry.bin file");
			//Console.WriteLine("         -clean            Keep the geometry.bin file clean");
			//Console.WriteLine("         -nobanner         Don't display the startup banner");
			Console.WriteLine("         -verbose          Show verbose output");
			Console.WriteLine("         -nowait           Don't wait after conversion");
			Console.WriteLine("         -help/-h/-?       Display this help screen");
			Console.WriteLine();
		}

		public static void VerboseOutput(string output)
		{
			if (Options["verbose"] != null)
				Console.WriteLine("(verbose) " + output);
		}

		public static void ErrorOutput(string output)
		{
			Console.WriteLine("(error)   " + output);
		}
		
		public static void WarningOutput(string output)
		{
			Console.WriteLine("(warning) " + output);
		}

		[STAThread]
		static int Main(string[] args)
		{
			if (!Options.CollectOptions(args))
			{
				PrintBanner();
				PrintUsage();
				return 1;
			} 
			else
			{
				//if (Options["nobanner"] == null)
				PrintBanner();

				VerboseOutput("Source: " + Options["source"]);
				VerboseOutput("Target: " + Options["target"]);

				Options.CollectExtraOptions();

				VerboseOutput("X-Name: " + Options["xname"]);

				VerboseOutput("Loading source...");

				DataCollector dc = new DataCollector();
				RealGeometryFile rgf = null;

				FileInfo fileInfo = new FileInfo(Options["source"]);
				if (fileInfo.Extension.ToLower() == ".mwr") 
				{
					RawGeometryFile rg = new RawGeometryFile();
					rg.Read(Options["source"]);
					VerboseOutput("Collecting data [Most Wanted Raw]...");
					try
					{
						rgf = dc.Collect(rg);
					} 
					catch (Exception e)
					{
						ErrorOutput("Exception: " + e.Message);
						return 1;
					}
				} 
				else if (fileInfo.Extension.ToLower() == ".ase")
				{
					AseFile ase = new AseFile();
					try
					{
						ase.Open(Options["source"]);
					}
					catch (Exception e)
					{
						ErrorOutput("ASE Exception: " + e.Message);
						return 1;
					}
					
					VerboseOutput("Collecting data [Ascii Scene Export]...");
					try
					{
						rgf = dc.Collect(ase);
					} 
					catch (Exception e)
					{
						ErrorOutput("Exception: " + e.Message);
					}					
				} 
				else
				{
					ErrorOutput("Unsupported source file extension.");
					return 1;					
				}
				
				if (rgf == null)
				{
					ErrorOutput("Failed to collect data.");
					return 1;
				} 
				else
				{
					VerboseOutput("Writing target file...");
					rgf.Save(Options["target"]);
					VerboseOutput("Completed successfully.");
				}

			}

			if (Options["nowait"] == null)
			{
				Console.WriteLine();
				Console.WriteLine("Press any key to continue...");
				Console.ReadLine();
			}

			return 0;
				
		}
	}
	class CompilerOptions
	{
		Hashtable options = new Hashtable();

		public string this[string key]
		{
			get
			{
				if (options.ContainsKey(key))
					return options[key].ToString();
				else
					return null;
			}
			set
			{
				if (options.ContainsKey(key))
					options[key] = value;
				else
					options.Add(key, value);
			}
			
		}

		public bool CollectOptions(string[] args)
		{
			Queue q = new Queue(args);
			bool endOptions = false;
			ArrayList multiparamOptions = new ArrayList(new string[] {"xname","matlist","xlink","source","target"});

			try
			{
				do
				{
					string item = "";
					if (q.Count > 0)
						item = q.Dequeue().ToString();
					else
						endOptions = true;

					if (!endOptions && (item[0] == '-' || item[0] == '/'))
					{
						string optName = item.Substring(1);
						if (optName == "-")
							endOptions = true;
						else
						{
							if (multiparamOptions.Contains(optName))
								options.Add(optName, q.Dequeue().ToString());
							else
								options.Add(optName, 1);							
						}
					} 
					else
					{
						endOptions = true;
						if (q.Count > 1)
							return false;
						if (!options.ContainsKey("source"))
							options.Add("source", item);
						if (q.Count > 0)
							options.Add("target", q.Dequeue().ToString());
					}
				} while (q.Count > 0);

				if (!options.ContainsKey("target"))
					options.Add("target", "geometry.bin");

				if (!options.ContainsKey("source") || !options.ContainsKey("target")) 
					return false;

				if (options.ContainsKey("help") || options.ContainsKey("h") 
					|| options.ContainsKey("?"))
						return false;
				
			} 
			catch
			{
				return false;
			}

			return true;
		}

		public void CollectExtraOptions()
		{
			if (this["xname"] == null)
			{
				string xname;
				Console.Write("X-Name: ");
                xname = Console.ReadLine();
				this["xname"] = xname;
			}
		}
	}
}
