#define GAME_NFSMW
//#define GAME_NFSU2

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace mwgc.RealEngine
{

	public enum RealType : uint
	{
		Null = 0,
		
		Geometry = 0x80134000,
		// {
		GeometryParts = 0x80134001,
		// {
		GeometryPartsDesc = 0x00134002,
		GeometryPartsHash = 0x00134003,
		GeometryPartsOffset = 0x00134004,
		GeometryPartsEmpty = 0x80134008, 
		// }
		GeometryPart = 0x80134010,
		// {
		GeometryPartDesc = 0x00134011,
		GeometryPartTextures = 0x00134012,
		GeometryPartShaders = 0x00134013,
		// 0x0013401A {hash, 0, 0, 0, d3dmatrix}
		GeometryPartMountPoints = 0x0013401A,
		GeometryPartData = 0x80134100,
		// {
		GeometryPartDataDesc = 0x00134900,
		GeometryPartDataVertices = 0x00134B01,
		GeometryPartDataGroups	= 0x00134B02,
		GeometryPartDataIndices = 0x00134B03,
		GeometryPartDataMaterialName = 0x00134C02
		// ?? 0x00134017
		// }
		// ?? 0x00134017
		// ?? 0x00134018
		// ?? 0x00134019
		// }
		// }
	}

	public class RealChunk
	{
		protected int _offset;
		protected RealType _type;
		protected int _length;

		public bool IsParent
		{
			get { return ((int)_type & 0x80000000) != 0; }
		}

		public RealType Type
		{
			get { return _type; }
			set { _type = value; }
		}

		public int EndOffset
		{
			get { return _offset + _length + 0x8; }
			set { _length = value - _offset - 0x8; }
		}

		public int Offset
		{
			get { return _offset; }
			set { _offset = value; }
		}

		public int Length
		{
			get { return _length; }
			set { _length = value; }
		}

		public int RawLength
		{
			get { return Length+0x8; }
		}

		public void Read(BinaryReader br)
		{
			_offset = (int)br.BaseStream.Position;
			_type = (RealType)br.ReadUInt32();
			_length = br.ReadInt32();
		}

		public void Write(BinaryWriter bw)
		{
			long offset = bw.BaseStream.Position;
			bw.BaseStream.Seek(_offset, SeekOrigin.Begin);
			bw.Write((uint)_type);
			bw.Write(_length);
			bw.BaseStream.Seek(offset, SeekOrigin.Begin);
		}

		public void GoToStart(Stream fs)
		{
			fs.Seek(_offset+8, SeekOrigin.Begin);
		}

		public void Skip(Stream fs)
		{
			fs.Seek(EndOffset, SeekOrigin.Begin);
		}

		public override string ToString()
		{
			return "RealChunk {\n\tType=" + String.Format("{0:X}", _type) 
				+ ",\n\tOffset=" + String.Format("{0:X}", _offset)
				+ ",\n\tLength=" + String.Format("{0:X}", _length)
				+ "\n}";
		}

	}

	public struct FixedLenString
	{
		int _length;
		string _string;
		
		public FixedLenString(string data)
		{
			_length = data.Length + 4 - (data.Length % 4);
			_string = data;
		}

		public FixedLenString(string data, int length)
		{
			_length = length;
			_string = data;
		}

		public FixedLenString(BinaryReader br, int length)
		{
			_length = 0;
			_string = "";
			Read(br, length);
		}
		public FixedLenString(BinaryReader br)
		{
			_length = 0;
			_string = "";
			Read(br);
		}
		public void Read(BinaryReader br, int length)
		{
			_length = length;
			byte[] bytes = br.ReadBytes(length);
			string data = Encoding.ASCII.GetString(bytes);
			_string = data.TrimEnd(new char[] {(char)0});
		}
		public void Read(BinaryReader br)
		{
			_length = 0;
			_string = "";
			while(true)
			{
				byte[] bytes = br.ReadBytes(4);
				string data = Encoding.ASCII.GetString(bytes);
				_string += data;
				_length += 4;
				if (data.IndexOf((char)0) >= 0)
				{
					_string = _string.TrimEnd(new char[] {(char)0});
					break;
				}
			}
			
		}
		public void Write(BinaryWriter bw)
		{
			string data = _string.PadRight(_length, (char)0);
			byte[] bytes = Encoding.ASCII.GetBytes(data);
			bw.Write(bytes);
		}
		public override string ToString()
		{
			return _string;
		}

	}

	public struct RealVector2 
	{
		public float u,v;
		public RealVector2(float u, float v) 
		{
			this.u=u;
			this.v=v;
		}
		public void Read(BinaryReader reader) 
		{
			u = reader.ReadSingle();
			v = reader.ReadSingle();
		}
		public void Write(BinaryWriter writer) 
		{
			writer.Write(u);
			writer.Write(v);
		}
		public override string ToString()
		{
			return "Vector2 {" + u + "," + v + "}";
		}
		public override int GetHashCode()
		{
			string hash = u.GetHashCode() + ":" + v.GetHashCode();
			return hash.GetHashCode();
		}
	}

	public struct RealVector3 
	{
		public float x,y,z;
		public RealVector3(float x, float y, float z) 
		{
			this.x=x;
			this.y=y;
			this.z=z;
		}			
		public void Read(BinaryReader reader) 
		{
			x = reader.ReadSingle();
			y = reader.ReadSingle();
			z = reader.ReadSingle();
		}
		public void Write(BinaryWriter writer) 
		{
			writer.Write(x);
			writer.Write(y);
			writer.Write(z);
		}
		public override string ToString()
		{
			return "Vector3 {" + x + "," + y + "," + z + "}";
		}		
		public override int GetHashCode()
		{
			string hash = x.GetHashCode() + ":" + y.GetHashCode() + ":" + z.GetHashCode();
			return hash.GetHashCode();
		}
	}

	public struct RealVector4
	{
		public float x,y,z,w;
		public RealVector4(float x, float y, float z, float w)
		{
			this.x=x;
			this.y=y;
			this.z=z;
			this.w=w;
		}	
		public RealVector4(float x, float y, float z)
		{
			this.x=x;
			this.y=y;
			this.z=z;
			this.w=0.0f;
		}
		public void Read(BinaryReader reader) 
		{
			x = reader.ReadSingle();
			y = reader.ReadSingle();
			z = reader.ReadSingle();
			w = reader.ReadSingle();
		}
		public void Write(BinaryWriter writer) 
		{
			writer.Write(x);
			writer.Write(y);
			writer.Write(z);
			writer.Write(w);
		}
		public override string ToString()
		{
			return "Vector4 {" + x + "," + y + "," + z + "," + w + "}";
		}		

	}

	public struct RealMatrix 
	{
		public float[] m;
		public float Get(int i, int j) 
		{
			return m[i*4+j];
		}
		public void Set(int i, int j, float newValue) 
		{
			m[i*4+j]=newValue;
		}
		public void Read(BinaryReader reader) 
		{
			m = new float[16];
			for (int i=0; i<16; i++)
				m[i]=reader.ReadSingle();
		}
		public void Write(BinaryWriter writer) 
		{
			for (int i=0; i<16; i++)
				writer.Write(m[i]);
		}

	}

	public struct RealVertex
	{
		public RealVector3 Position;
		public RealVector3 Normal;
		public int Diffuse;
		public RealVector2 UV;
		public RealVector2 UV1, UV2, UV3;

		private bool _withNormal;
		private int _maxUV;

		public void Initialize(bool withNormal, int maxUV)
		{
			_withNormal = withNormal;
			_maxUV = maxUV;
		}

		public void Read(BinaryReader reader, bool withNormal) 
		{
			Read(reader, withNormal, 0);
		}
		
		public void Read(BinaryReader reader, bool withNormal, int maxUV) 
		{
			_withNormal = withNormal;
			_maxUV = maxUV;
			Position.Read(reader);
			if (_withNormal)
				Normal.Read(reader);
			Diffuse = reader.ReadInt32();
			UV.Read(reader);
			if (_maxUV > 0)
				UV1.Read(reader);
			if (_maxUV > 1)
				UV2.Read(reader);
			if (_maxUV > 2)
				UV3.Read(reader);
		}
		
		public void Write(BinaryWriter writer) 
		{
			Position.Write(writer);
			if (_withNormal)
				Normal.Write(writer);
			writer.Write(Diffuse);
			UV.Write(writer);
			if (_maxUV > 0)
				UV1.Write(writer);
			if (_maxUV > 1)
				UV2.Write(writer);
			if (_maxUV > 2)
				UV3.Write(writer);
		}

		public override int GetHashCode()
		{
			string hash = Position.GetHashCode() + ":" + Normal.GetHashCode() + ":" + 
				Diffuse.GetHashCode() + ":" + UV.GetHashCode() + ":" + UV1.GetHashCode() + ":" +
				UV2.GetHashCode() + ":" + UV3.GetHashCode();
			return hash.GetHashCode();
		}
	}


	public struct RealMountPoint
	{
		public uint Hash;
		public int Null1;
		public int Null2;
		public int Null3;
		public RealMatrix Transform;
		
		public void Read(BinaryReader reader) 
		{
			Hash = reader.ReadUInt32();
			Null1 = reader.ReadInt32();
			Null2 = reader.ReadInt32();
			Null3 = reader.ReadInt32();
			Transform.Read(reader);
		}
		
		public void Write(BinaryWriter writer) 
		{
			writer.Write(Hash);
			writer.Write(Null1);
			writer.Write(Null2);
			writer.Write(Null3);
			Transform.Write(writer);
		}
	}

	public struct RealShadingGroup
	{
		// the definition order of this chunk is no longer in spec with the format
		// look at the read/write routines to figure out the def. order

		public RealVector3 BoundsMin;
		public int Length;
		public RealVector3 BoundsMax;
#if GAME_NFSU2
		private int _textureIndex;
		private int _shaderIndex;
		public int D1, D2, D3, D4;
#endif
		public int Offset;

		// 0x010000 means it uses some extension chunk data (0x00134017 18 19)
		// 0xA20000 means it uses normal map (texture) + overwrite > Z + alphablendenabnle
		// 0x000100 means use the normals data (enable lighting)
		// @also see PartInfo flags
		
		// Cars: 0x4180
		// Platform: 0x4000
		public int Flags;

#if GAME_NFSMW
		// pseudo order in comments

		// minVec3, maxVec3
		public byte TextureIndex0;
		public byte TextureIndex1;	// normal map
		public byte TextureIndex2;
		public byte TextureIndex3;
		public byte TextureIndex4;
		public byte ShaderIndex0;
		public short Padding1;
		public int Null1, Null2, Null3, Null4;
		public int Unk1;	// Cars: 0x4, Platform: 0x0 - 1 uv, 0x3 - 4 uv (maximum texture index)
		public int Null5;
		// flags here
		public int VertexCount;
		public int TriangleCount;
		// offset here
		public int Null6, Null7, Null8, Null9, Null10;
		// length here
		public int Null11, Null12;

#endif

		public int TextureIndex
		{
#if GAME_NFSU2
			get { return _textureIndex; }
			set { _textureIndex = value; }
#else
			get { return TextureIndex0; }
			set { return; }
#endif
		}

		public int ShaderIndex
		{
#if GAME_NFSU2
			get { return _shaderIndex; }
			set { _shaderIndex = value; }
#else
			get { return ShaderIndex0; }
			set { return; }
#endif
		}

		public bool UseNormalMap
		{
			get { return (Flags & 0xA20000) != 0; }
		}

		public bool EnableLighting
		{
			get { return (Flags & 0x000100) != 0; }
		}


		public void Read(BinaryReader reader) 
		{
#if GAME_NFSU2
			BoundsMin.Read(reader);
			Length = reader.ReadInt32();
			BoundsMax.Read(reader);
			TextureIndex = reader.ReadInt32();
			ShaderIndex = reader.ReadInt32();
			D1 = reader.ReadInt32();
			D2 = reader.ReadInt32();
			D3 = reader.ReadInt32();
			D4 = reader.ReadInt32();
			Offset = reader.ReadInt32();
			Flags = reader.ReadInt32();
#endif
#if GAME_NFSMW
			BoundsMin.Read(reader);
			BoundsMax.Read(reader);
			TextureIndex0 = reader.ReadByte();
			TextureIndex1 = reader.ReadByte();
			TextureIndex2 = reader.ReadByte();
			TextureIndex3 = reader.ReadByte();
			TextureIndex4 = reader.ReadByte();
			ShaderIndex0 = reader.ReadByte();
			Padding1 = reader.ReadInt16();
			Null1 = reader.ReadInt32();
			Null2 = reader.ReadInt32();
			Null3 = reader.ReadInt32();
			Null4 = reader.ReadInt32();
			Unk1 = reader.ReadInt32();
			Null5 = reader.ReadInt32();
			Flags = reader.ReadInt32();
			VertexCount = reader.ReadInt32();
			TriangleCount = reader.ReadInt32();
			Offset = reader.ReadInt32();
			Null6 = reader.ReadInt32();
			Null7 = reader.ReadInt32();
			Null8 = reader.ReadInt32();
			Null9 = reader.ReadInt32();
			Null10 = reader.ReadInt32();
			Length = reader.ReadInt32();
			Null11 = reader.ReadInt32();
			Null12 = reader.ReadInt32();
#endif
		}
		
		public void Write(BinaryWriter writer) 
		{
#if GAME_NFSU2
			BoundsMin.Write(writer);
			writer.Write(Length);
			BoundsMax.Write(writer);
			writer.Write(TextureIndex);
			writer.Write(ShaderIndex);
			writer.Write(D1);
			writer.Write(D2);
			writer.Write(D3);
			writer.Write(D4);
			writer.Write(Offset);
			writer.Write(Flags);
#endif
#if GAME_NFSMW
			BoundsMin.Write(writer);
			BoundsMax.Write(writer);
			writer.Write(TextureIndex0);
			writer.Write(TextureIndex1);
			writer.Write(TextureIndex2);
			writer.Write(TextureIndex3);
			writer.Write(TextureIndex4);
			writer.Write(ShaderIndex0);
			writer.Write(Padding1);
			writer.Write(Null1);
			writer.Write(Null2);
			writer.Write(Null3);
			writer.Write(Null4);
			writer.Write(Unk1);
			writer.Write(Null5);
			writer.Write(Flags);
			writer.Write(VertexCount);
			writer.Write(TriangleCount);
			writer.Write(Offset);
			writer.Write(Null6);
			writer.Write(Null7);
			writer.Write(Null8);
			writer.Write(Null9);
			writer.Write(Null10);
			writer.Write(Length);
			writer.Write(Null11);
			writer.Write(Null12);
#endif
		}
	}
	public class RealGeometryInfo
	{
		public int Null1, Null2;
		public int Unk1;			// 0x1C [MW: 0x1D]
		public int PartCount;
		public FixedLenString RelFilePath; //[0x38];
		public FixedLenString ClassType;	//[0x20];
		public int ExtChunkOffs;	// chunk id = 80034020
		public int ExtChunkLen;
		public int Unk2;			// 0x80
		public int Null3, Null4, Null5;
#if GAME_NFSMW
		public int Null6_MW, Null7_MW, Null8_MW, Null9_MW;
#endif
		public void Read(BinaryReader reader) 
		{
			Null1 = reader.ReadInt32();
			Null2 = reader.ReadInt32();
			Unk1 = reader.ReadInt32();
			PartCount = reader.ReadInt32();
			RelFilePath = new FixedLenString(reader, 0x38);
			ClassType = new FixedLenString(reader, 0x20);
			ExtChunkOffs = reader.ReadInt32();
			ExtChunkLen = reader.ReadInt32();
			Unk2 = reader.ReadInt32();
			Null3 = reader.ReadInt32();
			Null4 = reader.ReadInt32();
			Null5 = reader.ReadInt32();
#if GAME_NFSMW			
			Null6_MW = reader.ReadInt32();
			Null7_MW = reader.ReadInt32();
			Null8_MW = reader.ReadInt32();
			Null9_MW = reader.ReadInt32();
#endif
		}
		public void Write(BinaryWriter writer) 
		{
			writer.Write(Null1);
			writer.Write(Null2);
			writer.Write(Unk1);
			writer.Write(PartCount);
			RelFilePath.Write(writer);
			ClassType.Write(writer);
			writer.Write(ExtChunkOffs);
			writer.Write(ExtChunkLen);
			writer.Write(Unk2);
			writer.Write(Null3);
			writer.Write(Null4);
			writer.Write(Null5);
#if GAME_NFSMW			
			writer.Write(Null6_MW);
			writer.Write(Null7_MW);
			writer.Write(Null8_MW);
			writer.Write(Null9_MW);
#endif
		}
	}

	public struct RealGeometryPartInfo
	{
		public int Null1, Null2, Null3;
		public int Unk1;	// 0x00400013	[MW: 0x00400018]
		public uint Hash;
		public int TriangleCount;
		public byte Null4, TextureCount;
		public byte ShaderCount, Null5;
		public int Null6;
		public RealVector4 BoundMin;
		public RealVector4 BoundMax;
		public RealMatrix Transform;
		public int Null7, Null8;
		public int Unk2, Unk3;		// both 0x0012F800	[U2: 0x000EE580] [MW: 0x000EA550]
		public int Null9;

#if GAME_NFSU2
		// for NFSU2 only
		public float Unk4, Unk5;
		public int Unk6, Unk7;
#endif
#if GAME_NFSMW
		// for MW only
		public int Unk4_MW;
		// unk5 and unk6 are like quality settings for LODing. 
		// unk5 * unk6 = triangle count
		// unk6 is decreasing for lower LOD
		public float Unk5_MW, Unk6_MW;
#endif

		public FixedLenString PartName;	//[0x1C] for U2, [??? align4] for MW;

		// external links
		public uint[] Textures;
		public uint[] Shaders;
		public RealMountPoint[] MountPoints;

		public void Read(BinaryReader reader) 
		{
			Null1 = reader.ReadInt32();
			Null2 = reader.ReadInt32();
			Null3 = reader.ReadInt32();
			Unk1 = reader.ReadInt32();
			Hash = reader.ReadUInt32();
			TriangleCount = reader.ReadInt32();
			Null4 = reader.ReadByte();
			TextureCount = reader.ReadByte();
			ShaderCount = reader.ReadByte();
			Null5 = reader.ReadByte();
			Null6 = reader.ReadInt32();
			BoundMin.Read(reader);
			BoundMax.Read(reader);
			Transform.Read(reader);
			Null7 = reader.ReadInt32();
			Null8 = reader.ReadInt32();
			Unk2 = reader.ReadInt32();
			Unk3 = reader.ReadInt32();
			Null9 = reader.ReadInt32();

#if GAME_NFSU2
			// U2 fix
			Unk4 = reader.ReadSingle();
			Unk5 = reader.ReadSingle();
			Unk6 = reader.ReadInt32();
			Unk7 = reader.ReadInt32();
#endif
#if GAME_NFSMW
			Unk4_MW = reader.ReadInt32();
			Unk5_MW = reader.ReadSingle();
			Unk6_MW = reader.ReadSingle();
#endif

#if GAME_NFSMW
			PartName = new FixedLenString(reader);
#else
			PartName = new FixedLenString(reader, 0x1C);
#endif
		}

		public void Write(BinaryWriter writer) 
		{
			writer.Write(Null1);
			writer.Write(Null2);
			writer.Write(Null3);
			writer.Write(Unk1);
			writer.Write(Hash);
			writer.Write(TriangleCount);
			writer.Write(Null4);
			writer.Write(TextureCount);
			writer.Write(ShaderCount);
			writer.Write(Null5);
			writer.Write(Null6);
			BoundMin.Write(writer);
			BoundMax.Write(writer);
			Transform.Write(writer);
			writer.Write(Null7);
			writer.Write(Null8);
			writer.Write(Unk2);
			writer.Write(Unk3);
			writer.Write(Null9);

#if GAME_NFSU2
			writer.Write(Unk4);
			writer.Write(Unk5);
			writer.Write(Unk6);
			writer.Write(Unk7);
#endif
#if GAME_NFSMW
			writer.Write(Unk4_MW);
			writer.Write(Unk5_MW);
			writer.Write(Unk6_MW);
#endif

			PartName.Write(writer);
		}

		public void ReadTextures(BinaryReader reader)
		{
			Textures = new uint[TextureCount];
			for(int i=0; i<TextureCount; i++)
			{
				Textures[i] = reader.ReadUInt32();
				reader.ReadInt32();
			}
		}
		public void WriteTextures(BinaryWriter writer)
		{
			for(int i=0; i<TextureCount; i++)
			{
				writer.Write(Textures[i]);
				writer.Write(0);
			}			
		}
		public void ReadShaders(BinaryReader reader)
		{
			Shaders = new uint[ShaderCount];
			for(int i=0; i<ShaderCount; i++)
			{
				Shaders[i] = reader.ReadUInt32();
				reader.ReadInt32();
			}
		}
		public void WriteShaders(BinaryWriter writer)
		{
			for(int i=0; i<ShaderCount; i++)
			{
				writer.Write(Shaders[i]);
				writer.Write(0);
			}
		}
		public void ReadMountPoints(BinaryReader reader, RealChunk chunk)
		{
			int count = (chunk.EndOffset - (int)reader.BaseStream.Position) / 0x50;
			MountPoints = new RealMountPoint[count];
			for(int i=0; i<count; i++)
			{
				MountPoints[i] = new RealMountPoint();
				MountPoints[i].Read(reader);
			}
		}
		public void WriteMountPoints(BinaryWriter writer)
		{
			for(int i=0; i<MountPoints.Length; i++)
			{
				MountPoints[i].Write(writer);
			}
		}	
	}

	public struct RealGeometryPartData
	{
		public int Null1, Null2;
		public int Unk1;	// 0x10		[MW: 0x12]

		// 0x000080 -- has normals (doesnt really make sense anymore for MW)
		// 0x000100 -- exterior [alpha enabled?] part [license_plate, bottom is not considered to be one...]
		//			-- enable lighting?
		// 0x004000 -- dunno, there by default
		// 0x010000 -- has additional chunks (dont use!)
		// 0x020000 -- uses normal map
		// 0x200000 -- ??
		// 0x800000 -- ??
		public int Flags;	// some flags

		public int GroupCount;

#if GAME_NFSMW
		public int Null2_MW;
		public int VBCount;	//		[MW: 0x01]  --- number of vertex buffers
#endif
		public int Null3, Null4, Null5, Null6;
#if GAME_NFSU2
		private int _triangleCount;
		public int Null7, Null8, Null9;
		private int _vertexCount;
		public int Null10, Null11;
		public int Null12;
#endif
#if GAME_NFSMW
		private int _indexCount;
#endif

		public RealVertex[] Vertices;
		public ushort[] Indices;
		public RealShadingGroup[] Groups;

//#if GAME_NFSMW
		public ArrayList Materials;
//#endif

		public int IndexCount
		{
#if GAME_NFSU2
			get { return Indices.Length; }
			set { return; }
#else
			get { return _indexCount; }
			set { _indexCount = value; }
#endif
		}

		public bool HasNormals
		{
#if GAME_NFSU2
			get { return (Flags & 0x0080)!=0; }
#else
			get { return true; }
#endif
		}

// Compat functions for MW
		public int VertexCount
		{
#if GAME_NFSMW
			get
			{
				int count = 0;
				foreach(RealShadingGroup group in Groups)
				{
					count += group.VertexCount;
				}
				return count;
			}
#else
			get { return _vertexCount; }
			set { _vertexCount = value; }
#endif
		}

		public int TriangleCount
		{
#if GAME_NFSMW
			get
			{
				return IndexCount / 3;
			}
			set
			{
				IndexCount = value * 3;
			}
#else
			get { return _triangleCount; }
			set { _triangleCount = value; }
#endif
		}

		public void Read(BinaryReader reader) 
		{
			Null1 = reader.ReadInt32();
			Null2 = reader.ReadInt32();
			Unk1 = reader.ReadInt32();
			Flags = reader.ReadInt32();
			GroupCount = reader.ReadInt32();
#if GAME_NFSMW
			Null2_MW = reader.ReadInt32();
			VBCount = reader.ReadInt32();
#endif
			Null3 = reader.ReadInt32();
			Null4 = reader.ReadInt32();
			Null5 = reader.ReadInt32();
			Null6 = reader.ReadInt32();
#if GAME_NFSU2
			TriangleCount = reader.ReadInt32();
			Null7 = reader.ReadInt32();
			Null8 = reader.ReadInt32();
			Null9 = reader.ReadInt32();
			VertexCount = reader.ReadInt32();
			Null10 = reader.ReadInt32();
			Null11 = reader.ReadInt32();
			Null12 = reader.ReadInt32();
#endif			
#if GAME_NFSMW
			IndexCount = reader.ReadInt32();
#endif

		}
		public void Write(BinaryWriter writer) 
		{
			writer.Write(Null1);
			writer.Write(Null2);
			writer.Write(Unk1);
			writer.Write(Flags);
			writer.Write(GroupCount);
#if GAME_NFSMW
			writer.Write(Null2_MW);
			writer.Write(VBCount);
#endif
			writer.Write(Null3);
			writer.Write(Null4);
			writer.Write(Null5);
			writer.Write(Null6);
#if GAME_NFSU2
			writer.Write(TriangleCount);
			writer.Write(Null7);
			writer.Write(Null8);
			writer.Write(Null9);
			writer.Write(VertexCount);
			writer.Write(Null10);
			writer.Write(Null11);
			writer.Write(Null12);
#endif
#if GAME_NFSMW
			writer.Write(IndexCount);
#endif
		}

		public void ReadVertices(BinaryReader reader)
		{
			bool normals = HasNormals;
			if (normals == false)
			{
				normals = true;
			}
			Vertices = new RealVertex[VertexCount];
			for(int i=0; i<VertexCount; i++)
			{
				Vertices[i].Read(reader, normals);
			}
		}
		public void WriteVertices(BinaryWriter writer)
		{
			for(int i=0; i<VertexCount; i++)
			{
				Vertices[i].Write(writer);
			}
		}
		public void ReadIndices(BinaryReader reader)
		{
			Indices = new ushort[TriangleCount*3];
			for(int i=0; i<TriangleCount*3; i++)
			{
				Indices[i]=reader.ReadUInt16();
			}
		}
		public void WriteIndices(BinaryWriter writer)
		{
			for(int i=0; i<TriangleCount*3; i++)
			{
				writer.Write(Indices[i]);
			}
		}	
		public void ReadGroups(BinaryReader reader)
		{
			Groups = new RealShadingGroup[GroupCount];
			for(int i=0; i<GroupCount; i++)
			{
				Groups[i].Read(reader);
			}
		}
		public void WriteGroups(BinaryWriter writer)
		{
			for(int i=0; i<GroupCount; i++)
			{
				Groups[i].Write(writer);
			}
		}
	
		public void ReadMaterialName(BinaryReader reader)
		{
#if GAME_NFSMW
			if (Materials == null)
				Materials = new ArrayList();
			FixedLenString str = new FixedLenString(reader);
			Materials.Add(str);
#endif
		}
	}

	public class RealGeometryPart
	{
		public RealGeometryPartInfo PartInfo;
		public RealGeometryPartData PartData;

		public RealGeometryPart()
		{
			PartInfo = new RealGeometryPartInfo();
			PartData = new RealGeometryPartData();
		}
	}

	public abstract class RealFile
	{
		protected Stream _stream;
		protected BinaryReader _br;
		protected BinaryWriter _bw;
		protected Stack _chunkStack;

		protected void NextAlignment(int alignment)
		{
			if (_stream.Position % alignment != 0)
			{
				_stream.Position += (alignment - _stream.Position % alignment);
			}
		}

		protected void SkipChunk(RealChunk chunk)
		{
			chunk.Skip(_stream);
		}

		protected RealChunk NextChunk()
		{
			RealChunk chunk = new RealChunk();
			chunk.Read(_br);
			/*
			if (!Enum.IsDefined(typeof(RealType), chunk.Type)) {
				System.Diagnostics.Debug.WriteLine("Unknown Child Chunk Detected: " + chunk);
			}
			*/
			return chunk;
		}

		
		protected RealChunk BeginChunk(RealType type)
		{
			RealChunk chunk = new RealChunk();
			chunk.Offset = (int)_stream.Position;
			chunk.Type = type;
			_chunkStack.Push(chunk);
			_stream.Seek(0x8, SeekOrigin.Current);
			return chunk;
		}

		protected void EndChunk()
		{
			RealChunk chunk = _chunkStack.Pop() as RealChunk;
			chunk.EndOffset = (int)_stream.Position;
			chunk.Write(_bw);
		}

		public void Open(string filename)
		{
			FileStream fs = new FileStream(filename, FileMode.Open, FileAccess.Read);
			Open(fs);
		}

		public void Save(string filename)
		{
			FileStream fs = new FileStream(filename, FileMode.Create, FileAccess.Write);
			_stream = fs;
			_bw = new BinaryWriter(fs);
			_chunkStack = new Stack();
			ProcessSave();
			Close();
		}

		public void Open(Stream stream)
		{
			if (_stream != null)
				Close();

			_stream = stream;
			_stream.Seek(0, SeekOrigin.Begin);
			_br = new BinaryReader(_stream);
			ProcessOpen();
			Close();
		}

		private void Close()
		{
			if (_br != null)
				_br.Close();
			if (_bw != null)
				_bw.Close();
			_stream.Close();
			_chunkStack = null;
			_br = null;
			_bw = null;
			_stream = null;
		}

		protected abstract void ProcessOpen();
		protected abstract void ProcessSave();
	}

	public class RealGeometryFile : RealFile, IEnumerable
	{
		RealGeometryInfo _desc;
		ArrayList _parts;
		int _partIndex;

		public RealGeometryFile()
		{
			_parts = null;
			_desc = new RealGeometryInfo();
		}

		public RealGeometryInfo GeometryInfo
		{
			get
			{
				return _desc;
			}
			set
			{
				_desc = value;
			}
		}

		public RealGeometryPart this[int index]
		{
			get
			{
				return _parts[index] as RealGeometryPart;
			}
			set
			{
				_parts[index] = value;
			}
		}

		public RealGeometryPart this[uint hash]
		{
			get
			{
				foreach(RealGeometryPart part in _parts)
				{
					if (part.PartInfo.Hash == hash)
						return part;
				}
				return null;
			}
		}

		public int PartCount
		{
			get
			{
				return _parts.Count;
			}
		}

		public void AddPart(RealGeometryPart part)
		{
			if (_parts == null)
				_parts = new ArrayList();
			_partIndex = _parts.Count;
			_parts.Add(part);
		}

		public int FindPartIndex(uint hash)
		{
			for(int i=0; i<_parts.Count; i++)
				if ((_parts[i] as RealGeometryPart).PartInfo.Hash == hash)
					return i;
			return -1;
		}

		#region "Open code"

		void ProcessParentChunk(RealChunk parentChunk)
		{
			while(_stream.Position < parentChunk.EndOffset)
			{
				RealChunk chunk = NextChunk();
				if (chunk.IsParent)
				{
					switch(chunk.Type)
					{
						case RealType.Geometry:
							ProcessParentChunk(chunk);
							break;
						case RealType.GeometryParts:
							_parts = new ArrayList();
							ProcessParentChunk(chunk);
							break;
						case RealType.GeometryPart:
							_partIndex = _parts.Count;
							_parts.Add(new RealGeometryPart());
							ProcessParentChunk(chunk);
							break;
						case RealType.GeometryPartData:
							ProcessParentChunk(chunk);
							break;
						case RealType.GeometryPartsEmpty:
							ProcessParentChunk(chunk);
							break;
					}
				} 
				else
				{
					ProcessChildChunk(chunk);
				}
			}			
		}

		void ProcessChildChunk(RealChunk parentChunk)
		{
			switch(parentChunk.Type)
			{
				case RealType.Null:
					break;
				case RealType.GeometryPartsDesc:
					_desc.Read(_br);
					_parts = new ArrayList();
					break;
				case RealType.GeometryPartsHash:
					break;
				case RealType.GeometryPartsOffset:
					break;
				case RealType.GeometryPartDesc:
					NextAlignment(0x10);
					this[_partIndex].PartInfo.Read(_br);
					break;
				case RealType.GeometryPartTextures:
					this[_partIndex].PartInfo.ReadTextures(_br);
					break;
				case RealType.GeometryPartShaders:
					this[_partIndex].PartInfo.ReadShaders(_br);
					break;
				case RealType.GeometryPartMountPoints:
					NextAlignment(0x10);
					this[_partIndex].PartInfo.ReadMountPoints(_br, parentChunk);
					break;
				case RealType.GeometryPartDataDesc:
					NextAlignment(0x10);
					this[_partIndex].PartData.Read(_br);
					break;
				case RealType.GeometryPartDataVertices:
					NextAlignment(0x80);
					this[_partIndex].PartData.ReadVertices(_br);
					break;
				case RealType.GeometryPartDataGroups:
					NextAlignment(0x10);
					this[_partIndex].PartData.ReadGroups(_br);
					break;
				case RealType.GeometryPartDataIndices:
					NextAlignment(0x10);
					this[_partIndex].PartData.ReadIndices(_br);
					break;
				case RealType.GeometryPartDataMaterialName:
					this[_partIndex].PartData.ReadMaterialName(_br);
					break;
			}
			SkipChunk(parentChunk);
		}

		protected override void ProcessOpen()
		{
			while(_stream.Position < _stream.Length)
			{
				RealChunk chunk = NextChunk();
				if (chunk.IsParent)
				{
					ProcessParentChunk(chunk);
				} 
				else
				{
					ProcessChildChunk(chunk);
				}
			}
		}

		#endregion

		#region "Save code"

		private void PaddingAlignment(int padding)
		{
			if (_stream.Position % padding != 0)
			{
				BeginChunk(RealType.Null);
				if (_stream.Position % padding != 0)
				{
					int offset = (int)(padding - _stream.Position % padding);
					_stream.Seek(offset, SeekOrigin.Current);
				}
				EndChunk();
			}
		}

		protected override void ProcessSave()
		{
			RealChunk offsChunk;
			RealChunk[] partChunks = new RealChunk[_parts.Count];
			ArrayList hashes = new ArrayList();

			BeginChunk(RealType.Geometry);
			// {
			PaddingAlignment(0x10);

			BeginChunk(RealType.GeometryParts);
			// {
			BeginChunk(RealType.GeometryPartsDesc);
			_desc.Write(_bw);
			EndChunk();
			
			BeginChunk(RealType.GeometryPartsHash);
			for(int i=0; i<_parts.Count; i++)
			{
				RealGeometryPart part = _parts[i] as RealGeometryPart;
				hashes.Add(part.PartInfo.Hash);
			}
			hashes.Sort();
			for(int i=0; i<hashes.Count; i++)
			{
				_bw.Write((uint)hashes[i]);
				_bw.Write((uint)0);
			}
			EndChunk();

			offsChunk = BeginChunk(RealType.Null);
			_stream.Seek(_parts.Count * 0x4 * 6, SeekOrigin.Current);
			EndChunk();

			BeginChunk(RealType.GeometryPartsEmpty);
			EndChunk();

			// }
			EndChunk();

			for(int i=0; i<_parts.Count; i++)
			{
				PaddingAlignment(0x80);
				
				RealGeometryPart part = _parts[i] as RealGeometryPart;
				partChunks[i] = BeginChunk(RealType.GeometryPart);
				// {

				BeginChunk(RealType.GeometryPartDesc);
				NextAlignment(0x10);
				part.PartInfo.Write(_bw);
				EndChunk();

				BeginChunk(RealType.GeometryPartTextures);
				part.PartInfo.WriteTextures(_bw);
				EndChunk();

				BeginChunk(RealType.GeometryPartShaders);
				part.PartInfo.WriteShaders(_bw);
				EndChunk();

				if (part.PartInfo.MountPoints != null && part.PartInfo.MountPoints.Length > 0)
				{
					BeginChunk(RealType.GeometryPartMountPoints);
					NextAlignment(0x10);
					part.PartInfo.WriteMountPoints(_bw);
					EndChunk();					
				}

				BeginChunk(RealType.GeometryPartData);
				// {

				BeginChunk(RealType.GeometryPartDataDesc);
				NextAlignment(0x10);
				part.PartData.Write(_bw);
				EndChunk();

				BeginChunk(RealType.GeometryPartDataGroups);
				NextAlignment(0x10);
				part.PartData.WriteGroups(_bw);
				EndChunk();

				BeginChunk(RealType.GeometryPartDataIndices);
				NextAlignment(0x10);
				part.PartData.WriteIndices(_bw);
				EndChunk();

				BeginChunk(RealType.GeometryPartDataVertices);
				NextAlignment(0x80);
				part.PartData.WriteVertices(_bw);
				EndChunk();
				
				// }
				EndChunk();

				// }
				EndChunk();
			}

			// }
			EndChunk();

			// now lets redo the offsets
			_stream.Seek(offsChunk.Offset, SeekOrigin.Begin);
			BeginChunk(RealType.GeometryPartsOffset);
			for(int i=0; i<_parts.Count; i++)
			{
				RealGeometryPart part = _parts[i] as RealGeometryPart;
				int position = hashes.IndexOf(part.PartInfo.Hash);
				_stream.Seek(offsChunk.Offset + 0x8 + position * 0x4 * 6, SeekOrigin.Begin);
				_bw.Write(part.PartInfo.Hash);
				_bw.Write(partChunks[i].Offset);
				_bw.Write(partChunks[i].Length + 0x8);
				_bw.Write(partChunks[i].Length + 0x8);
				_bw.Write((uint)0);
				_bw.Write((uint)0);
			}
			_stream.Seek(offsChunk.EndOffset, SeekOrigin.Begin);
			EndChunk();

			_stream.Seek(_stream.Length, SeekOrigin.Begin);

			PaddingAlignment(0x1000);

			_stream.SetLength(_stream.Position);

			Debug.Assert(_chunkStack.Count == 0, "Internal error in save code.");
		}

		#endregion

		#region IEnumerable Members

		public IEnumerator GetEnumerator()
		{
			return _parts.GetEnumerator();
		}

		#endregion
	}

}
