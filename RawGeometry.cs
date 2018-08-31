using System;
using System.IO;
using System.Text;

namespace mwgc.RawGeometry
{
	public struct RawString
	{
		public int Length;
		public string Data;

		public void Read(BinaryReader br)
		{
			Length = br.ReadInt32();
			Data = Encoding.ASCII.GetString(br.ReadBytes(Length)).Split((char)0)[0];
		}

		public RawString(BinaryReader br)
		{
			Length = 0;
			Data = "";
			Read(br);
		}

		public override string ToString()
		{
			return Data;
		}

	}

	public struct RawObjectHeader
	{
		public RawString ObjName;
		public int NumVertices;
		public int NumFaces;
		public float[] Transform;

		public void Read(BinaryReader br)
		{
			ObjName = new RawString(br);
			NumVertices = br.ReadInt32();
			NumFaces = br.ReadInt32();
			Transform = new float[16];
			for(int i=0; i<16; i++)
				Transform[i] = br.ReadSingle();
		}

	}

	public struct RawHeader
	{
		public int Magic;
		public int NumMaterials;
		public int NumObjects;
		public RawString[] MatNames;
		public RawObjectHeader[] ObjHeaders;

		public void Read(BinaryReader br)
		{
			Magic = br.ReadInt32();
			NumMaterials = br.ReadInt32();
			NumObjects = br.ReadInt32();
			MatNames = new RawString[NumMaterials];
			for(int i=0; i<NumMaterials; i++)
				MatNames[i] = new RawString(br);
			ObjHeaders = new RawObjectHeader[NumObjects];
			for(int i=0; i<NumObjects; i++)
			{
				ObjHeaders[i] = new RawObjectHeader();
				ObjHeaders[i].Read(br);
			}
				
		}
	}

	public struct RawVertex 
	{
		public float X,Y,Z;
		public float nX,nY,nZ;

		public void Read(BinaryReader br)
		{
			X = br.ReadSingle();
			Y = br.ReadSingle();
			Z = br.ReadSingle();
			nX = br.ReadSingle();
			nY = br.ReadSingle();
			nZ = br.ReadSingle();
		}
	}

	public struct RawFace 
	{
		public int MatIndex;
		public short I1,I2,I3;
		public float tU1, tU2, tU3;
		public float tV1, tV2, tV3;

		public void Read(BinaryReader br)
		{
			MatIndex = br.ReadInt32();
			I1 = br.ReadInt16();
			I2 = br.ReadInt16();
			I3 = br.ReadInt16();
			br.ReadInt16();
			tU1 = br.ReadSingle();
			tU2 = br.ReadSingle();
			tU3 = br.ReadSingle();
			tV1 = br.ReadSingle();
			tV2 = br.ReadSingle();
			tV3 = br.ReadSingle();
		}
	}

	public class RawObject
	{
		public RawVertex[] Vertices;
		public RawFace[] Faces;

		public void Read(BinaryReader br, int numVertices, int numFaces)
		{
			Vertices = new RawVertex[numVertices];
			for(int i=0; i<numVertices; i++)
				Vertices[i].Read(br);
			Faces = new RawFace[numFaces];
			for(int i=0; i<numFaces; i++)
				Faces[i].Read(br);
		}
	}

	public class RawGeometryFile
	{
		public RawHeader Header;
		public RawObject[] Objects;

		public RawGeometryFile()
		{
		}

		public void Read(string filename)
		{

			FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
			BinaryReader br = new BinaryReader(fs);
			
			Header = new RawHeader();
			Header.Read(br);

			Objects = new RawObject[Header.NumObjects];
			for(int i=0; i<Header.NumObjects; i++)
			{
				Objects[i] = new RawObject();
				Objects[i].Read(br, Header.ObjHeaders[i].NumVertices, Header.ObjHeaders[i].NumFaces);
			}

			Compiler.VerboseOutput(string.Format("Loaded {0} materials", Header.NumMaterials));
			for(int i=0; i<Header.NumMaterials; i++)
				Compiler.VerboseOutput(string.Format(" + {0}", Header.MatNames[i].Data));

			Compiler.VerboseOutput(string.Format("Loaded {0} objects", Header.NumObjects));
			for(int i=0; i<Header.NumObjects; i++)
				Compiler.VerboseOutput(string.Format(" + {0}", Header.ObjHeaders[i].ObjName.Data));

			fs.Close();
		}
	}
}
