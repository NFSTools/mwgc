using System;
using System.Collections;
using System.Globalization;
using System.IO;
using mwgc.AseLib;
using mwgc.RawGeometry;
using mwgc.RealEngine;

namespace mwgc
{
	public class UniqueList
	{
		Hashtable ht;
		ArrayList list;
		public UniqueList()
		{
			ht = new Hashtable();
			list = new ArrayList();
		}
		public int Add(object item)
		{
			if (ht.ContainsKey(item))
				return (int)ht[item];
			else
			{
				int index = list.Add(item);
				ht.Add(item, index);
				return index;
			}				
		}
		public int Count
		{
			get { return list.Count; }
		}
		public object this[int index]
		{
			get
			{
				return list[index];
			}
			set
			{
				bool inHT = false;
				if (ht.ContainsKey(list[index]))
					inHT = true;
				if (inHT)
					ht.Remove(list[index]);
				list.RemoveAt(index);
				list.Insert(index, value);
				if (inHT)
					ht.Add(value, index);
			}
		}
	}

	public class DataCollector
	{
		private struct MatTexStringPair
		{
			public string Material;
			public string Texture;
		}

		private struct MatTexPair
		{
			public int Material;
			public int Texture;
			public override int GetHashCode()
			{
				string hash = Material.GetHashCode() + ":" + Texture.GetHashCode();
				return hash.GetHashCode();
			}

		}

		private class SubMesh
		{
			public int MaterialId;
			public int TextureId;
			public UniqueList VertexList;
			public ArrayList IndexList;

			public SubMesh()
			{
				VertexList = new UniqueList();
				IndexList = new ArrayList();
			}
		}

		private Hashtable materialHT;

		private void LoadCrossLinks(RealGeometryFile rgf)
		{

			if (Compiler.Options["xlink"] != null)
			{
				FileStream fs = new FileStream(Compiler.Options["xlink"], FileMode.Open, FileAccess.Read);
				StreamReader sr = new StreamReader(fs);
				string line;
				int lineNum = 0;
				do
				{
					lineNum++;
					line = sr.ReadLine();
					if (line != null)
					{
						if (line.StartsWith("#") || line.StartsWith("//") || line.StartsWith(";"))
						{
							// comment
						} 
						else
						{
							if (line.IndexOf('=') > -1)
							{
								string[] split = line.Split('=');
								string nameOrig = ResolveRealNameForce(split[0].Trim());
								uint hashOrig = RealHash(nameOrig);
								string nameTarget = ResolveRealNameForce(split[1].Trim());
								int partIndex = rgf.FindPartIndex(RealHash(nameTarget));
								
								if (partIndex > -1)
								{
									if (rgf.FindPartIndex(hashOrig) > -1)
									{
										Compiler.WarningOutput(string.Format(" + Part already exists: {0} at line {1}", split[1], lineNum));
									}
									else
									{
										Compiler.VerboseOutput(string.Format(" + Crosslink from {0} to {1}", split[0], split[1]));
										RealGeometryPart partTarget = rgf[partIndex];
										RealGeometryPart partOrig = new RealGeometryPart();
										partOrig.PartData = partTarget.PartData;
										partOrig.PartInfo = partTarget.PartInfo;

										partOrig.PartInfo.PartName = new FixedLenString(nameOrig);
										partOrig.PartInfo.Hash = hashOrig;

										rgf.AddPart(partOrig);										
									}
								} 
								else
								{
									Compiler.WarningOutput(string.Format(" + No such target part: {0} at line {1}", split[1], lineNum));
								}
							}
						}
					}
				}
				while (line != null);

				fs.Close();
			}

		}

		private void LoadMaterialHT()
		{
			Hashtable ht = new Hashtable();

			if (Compiler.Options["matlist"] != null)
			{
				FileStream fs = new FileStream(Compiler.Options["matlist"], FileMode.Open, FileAccess.Read);
				StreamReader sr = new StreamReader(fs);
				string line;
				do
				{
					line = sr.ReadLine();
					if (line != null)
					{
						if (line.StartsWith("#") || line.StartsWith("//") || line.StartsWith(";"))
						{
							// comment
						} 
						else
						{
							if (line.IndexOf('=') > -1)
							{
								string[] split = line.Split('=');
								ht.Add(split[0].Trim(), split[1].Trim());
							}
						}
					}
				}
				while (line != null);

				fs.Close();
			}

			materialHT = ht;
		}

		private static uint RealHash(string str)
		{
			uint hash = uint.MaxValue;
			unsafe
			{
				foreach(char c in str)
				{
					hash *= 33;
					hash += (uint)c;
				}
			}
			return hash;
		}

		private string ResolveRealNameForce(string precompileName)
		{
			string xname = Compiler.Options["xname"];
			string result = xname + "_" + precompileName;
			return result.ToUpper();
		}

		private string ResolveRealName(string precompileName)
		{
			string xname = Compiler.Options["xname"].ToUpper();
			string result = precompileName;
			if (precompileName.StartsWith("x_"))
				result = xname + precompileName.Substring(1);
			return result.ToUpper();
		}



		public RealGeometryFile Collect(RawGeometryFile rawGeom)
		{
			RealGeometryFile geom = new RealGeometryFile();
			Hashtable MatTex = new Hashtable();

			LoadMaterialHT();

			for (int i=0; i<rawGeom.Header.NumMaterials; i++)
			{
				MatTexStringPair pair = new MatTexStringPair();
				string mat = rawGeom.Header.MatNames[i].Data;
				if (materialHT.ContainsKey(mat))
					mat = (string)materialHT[mat];
				string[] split = mat.Split('/');
				if (split.Length < 2)
				{
					Compiler.WarningOutput(string.Format("Material {0} has invalid name.", mat));
					pair.Material = "DEFAULT";
					pair.Texture = "DEFAULT";
				} 
				else
				{
					pair.Material = split[0];
					pair.Texture = split[1];					
				}
				MatTex.Add(i, pair);
			}


			ArrayList mountPointObjects = new ArrayList();
			//ArrayList transparentObjects = new ArrayList(new string[] {"base_a","kit00_front_window_a","kit00_left_headlight_a"});
			ArrayList transparentObjects = new ArrayList();
			int diffuse;

			ArrayList basePartObjects = new ArrayList();

			for (int i=0; i<rawGeom.Header.NumObjects; i++)
			{
				Compiler.VerboseOutput(string.Format("Compiling object {0}: {1}", i+1, rawGeom.Header.ObjHeaders[i].ObjName));
				
				if (rawGeom.Header.ObjHeaders[i].ObjName.Data.StartsWith("#"))
				{
					string mountName;
					uint mountHash;
					mountName = rawGeom.Header.ObjHeaders[i].ObjName.Data.Substring(1).ToUpper();
					if (mountName.IndexOf("[") > -1)
					{
						mountName = mountName.Substring(0, mountName.IndexOf("["));
					}
					if (mountName.StartsWith("0x"))
						mountHash = uint.Parse(mountName.Substring(2), NumberStyles.HexNumber);
					else
						mountHash = RealHash(mountName);
					Compiler.VerboseOutput(string.Format(" + Mount Point Name: {0}", mountName));
					Compiler.VerboseOutput(string.Format(" + Compiled Hash: 0x{0:x}", mountHash));
                    RealMountPoint mp = new RealMountPoint();
					mp.Hash = mountHash;
					mp.Transform = new RealMatrix();
					mp.Transform.m = new float[16];
					float[] transform = rawGeom.Header.ObjHeaders[i].Transform;
					mp.Transform.m[0] = 1.0f;
					mp.Transform.m[5] = 1.0f;
					mp.Transform.m[10] = 1.0f;
					mp.Transform.m[15] = 1.0f;
					mp.Transform.m[12] = transform[14]; //z
					mp.Transform.m[13] = transform[12]; //x
					mp.Transform.m[14] = transform[13]; //y

                    mountPointObjects.Add(mp);
				} 
				else
				{
					Hashtable subMeshes = new Hashtable();
					UniqueList textures = new UniqueList();
					UniqueList materials = new UniqueList();

					if (transparentObjects.Contains(rawGeom.Header.ObjHeaders[i].ObjName.ToString()))
						diffuse = 0x7FFFFFFF;
					else
						diffuse = -1;

					for(int j=0; j<rawGeom.Header.ObjHeaders[i].NumFaces; j++) 
					{
						SubMesh subMesh;
						RawFace face = rawGeom.Objects[i].Faces[j];
						MatTexPair pair = new MatTexPair();
						MatTexStringPair strPair = (MatTexStringPair)MatTex[face.MatIndex];
						pair.Material = materials.Add(strPair.Material);
						pair.Texture = textures.Add(strPair.Texture);

						if (subMeshes.ContainsKey(pair))
							subMesh = subMeshes[pair] as SubMesh;
						else
						{
							subMesh = new SubMesh();
							subMesh.TextureId = pair.Texture;
							subMesh.MaterialId = pair.Material;
							subMeshes.Add(pair, subMesh);
						}
					
						// note ZM default = CCW, MW = CW
						RawVertex v1 = rawGeom.Objects[i].Vertices[face.I3];
						RawVertex v2 = rawGeom.Objects[i].Vertices[face.I2];
						RawVertex v3 = rawGeom.Objects[i].Vertices[face.I1];
					
						RealVertex rv;

						// v1
						rv = new RealVertex();
						rv.Initialize(true, 0);
					
						/*
						rv.Position.x =-v1.X;
						rv.Position.y = v1.Z;
						rv.Position.z = v1.Y;
						rv.Normal.x =-v1.nX;
						rv.Normal.y = v1.nZ;
						rv.Normal.z = v1.nY;
						*/
						rv.Position.x = v1.Z;
						rv.Position.y = v1.X;
						rv.Position.z = v1.Y;
						rv.Normal.x = v1.nZ;
						rv.Normal.y = v1.nX;
						rv.Normal.z = v1.nY;

						rv.Diffuse = diffuse;
						rv.UV.u = face.tU3;
						rv.UV.v = face.tV3;

						subMesh.IndexList.Add((ushort)subMesh.VertexList.Add(rv));

						// v2
						rv = new RealVertex();
						rv.Initialize(true, 0);
					
						/*
						rv.Position.x =-v2.X;
						rv.Position.y = v2.Z;
						rv.Position.z = v2.Y;
						rv.Normal.x =-v2.nX;
						rv.Normal.y = v2.nZ;
						rv.Normal.z = v2.nY;
						*/
						rv.Position.x = v2.Z;
						rv.Position.y = v2.X;
						rv.Position.z = v2.Y;
						rv.Normal.x = v2.nZ;
						rv.Normal.y = v2.nX;
						rv.Normal.z = v2.nY;

						rv.Diffuse = diffuse;
						rv.UV.u = face.tU2;
						rv.UV.v = face.tV2;

						subMesh.IndexList.Add((ushort)subMesh.VertexList.Add(rv));

						// v3
						rv = new RealVertex();
						rv.Initialize(true, 0);
					
						/*
						rv.Position.x =-v3.X;
						rv.Position.y = v3.Z;
						rv.Position.z = v3.Y;
						rv.Normal.x =-v3.nX;
						rv.Normal.y = v3.nZ;
						rv.Normal.z = v3.nY;
						*/
						rv.Position.x = v3.Z;
						rv.Position.y = v3.X;
						rv.Position.z = v3.Y;
						rv.Normal.x = v3.nZ;
						rv.Normal.y = v3.nX;
						rv.Normal.z = v3.nY;

						rv.Diffuse = diffuse;
						rv.UV.u = face.tU1;
						rv.UV.v = face.tV1;

						subMesh.IndexList.Add((ushort)subMesh.VertexList.Add(rv));

					}
			
					Compiler.VerboseOutput(string.Format(" + Compiled into {0} submeshes", subMeshes.Count));
					SubMesh[] subMeshList = new SubMesh[subMeshes.Count];
					subMeshes.Values.CopyTo(subMeshList, 0);
					for (int j=0; j<subMeshes.Count; j++)
					{
						SubMesh subMesh = subMeshList[j];

						Compiler.VerboseOutput(string.Format(" + Submesh {0}:", j+1));
						Compiler.VerboseOutput(string.Format("    + Material:  {0}", materials[subMesh.MaterialId]));
						Compiler.VerboseOutput(string.Format("    + Texture:   {0}", textures[subMesh.TextureId]));
						Compiler.VerboseOutput(string.Format("    + Vertices:  {0}", subMesh.VertexList.Count));
						Compiler.VerboseOutput(string.Format("    + Triangles: {0}", subMesh.IndexList.Count/3));
					}

					Compiler.VerboseOutput(string.Format("Creating part data for binary object file..."));

					RealGeometryPart part = new RealGeometryPart();

					//----- part info ----------

					RealVector4 boundsMin;
					RealVector4 boundsMax;

					if (rawGeom.Header.ObjHeaders[i].ObjName.Data.ToUpper().StartsWith("BASE_"))
						basePartObjects.Add(part);

					string resolvedName = ResolveRealNameForce(rawGeom.Header.ObjHeaders[i].ObjName.Data);

					part.PartInfo.Hash = RealHash(resolvedName);
					part.PartInfo.PartName = new FixedLenString(resolvedName);
					part.PartInfo.ShaderCount = (byte)materials.Count;
					part.PartInfo.Shaders = new uint[materials.Count];
					for (int j=0; j<materials.Count; j++)
					{
						string matName = materials[j] as string;
						if (matName.StartsWith("0x"))
							part.PartInfo.Shaders[j] = uint.Parse(matName.Substring(2), NumberStyles.HexNumber);
						else
							part.PartInfo.Shaders[j] = RealHash(matName);
					}

					part.PartInfo.TextureCount = (byte)textures.Count;
					part.PartInfo.Textures = new uint[textures.Count];
					for (int j=0; j<textures.Count; j++)
					{
						string texName = textures[j] as string;
						if (texName.StartsWith("0x"))
							part.PartInfo.Textures[j] = uint.Parse(texName.Substring(2), NumberStyles.HexNumber);
						else
							part.PartInfo.Textures[j] = RealHash(ResolveRealName(texName));
					}

					part.PartInfo.Transform.m = rawGeom.Header.ObjHeaders[i].Transform;
					/*
					part.PartInfo.Transform.m = new float[16];
					part.PartInfo.Transform.m[0] = 1.0f;
					part.PartInfo.Transform.m[5] = 1.0f;
					part.PartInfo.Transform.m[10] = 1.0f;
					part.PartInfo.Transform.m[15] = 1.0f;
					*/

					part.PartInfo.TriangleCount = rawGeom.Header.ObjHeaders[i].NumFaces;

					part.PartInfo.Unk1 = 0x00400018;
					part.PartInfo.Unk2 = 0x000EA550;
					part.PartInfo.Unk3 = 0x000EA550;

					part.PartInfo.Unk4_MW = 0;
					part.PartInfo.Unk5_MW = 1;
					part.PartInfo.Unk6_MW = part.PartInfo.TriangleCount;

					//----- part data ----------

					part.PartData.Flags = 0x000080 + /* 0x000100 */ + 0x004000 /* + 0x010000*/;
				
					part.PartData.GroupCount = subMeshList.Length;
					part.PartData.Groups = new RealShadingGroup[subMeshList.Length];

					int indexOffset = 0;
					for (int j=0; j<subMeshList.Length; j++)
					{
						SubMesh subMesh = subMeshList[j];
						part.PartData.Groups[j] = new RealShadingGroup();

						boundsMin = new RealVector4();
						boundsMax = new RealVector4();
						if (subMesh.VertexList.Count > 0)
						{
							RealVector3 v = ((RealVertex)subMesh.VertexList[0]).Position;
							boundsMin.x = v.x;
							boundsMin.y = v.y;
							boundsMin.z = v.z;
							boundsMax = boundsMin;
						}
						for(int k=1; k<subMesh.VertexList.Count; k++)
						{
							RealVector3 v = ((RealVertex)subMesh.VertexList[k]).Position;
							if (v.x > boundsMax.x)
								boundsMax.x = v.x;
							if (v.y > boundsMax.y)
								boundsMax.y = v.y;
							if (v.z > boundsMax.z)
								boundsMax.z = v.z;
							if (v.x < boundsMin.x)
								boundsMin.x = v.x;
							if (v.y < boundsMin.y)
								boundsMin.y = v.y;
							if (v.z < boundsMin.z)
								boundsMin.z = v.z;
						}
						part.PartData.Groups[j].BoundsMax = new RealVector3(boundsMax.x, boundsMax.y, boundsMax.z);
						part.PartData.Groups[j].BoundsMin = new RealVector3(boundsMin.x, boundsMin.y, boundsMin.z);

						part.PartData.Groups[j].Length = subMesh.IndexList.Count;
						part.PartData.Groups[j].TextureIndex0 = (byte)subMesh.TextureId;
						part.PartData.Groups[j].TextureIndex1 = (byte)subMesh.TextureId;
						part.PartData.Groups[j].TextureIndex2 = (byte)subMesh.TextureId;
						part.PartData.Groups[j].TextureIndex3 = (byte)subMesh.TextureId;
						part.PartData.Groups[j].TextureIndex4 = (byte)subMesh.TextureId;
						part.PartData.Groups[j].ShaderIndex0 = (byte)subMesh.MaterialId;

						part.PartData.Groups[j].Unk1 = 0x4;
						part.PartData.Groups[j].Flags = part.PartData.Flags;

						part.PartData.Groups[j].VertexCount = subMesh.VertexList.Count;
						part.PartData.Groups[j].TriangleCount = subMesh.IndexList.Count / 3;

						part.PartData.Groups[j].Offset = indexOffset;

						indexOffset += subMesh.IndexList.Count;
					
					}

					// -- bit of partinfo here -- caclulate bounds
					boundsMin = new RealVector4();
					boundsMax = new RealVector4();
					if (part.PartData.Groups.Length > 0)
					{
						RealVector3 v = part.PartData.Groups[0].BoundsMin;
						boundsMin.x = v.x;
						boundsMin.y = v.y;
						boundsMin.z = v.z;
						v = part.PartData.Groups[0].BoundsMax;
						boundsMax.x = v.x;
						boundsMax.y = v.y;
						boundsMax.z = v.z;
					}
					for (int j=1; j<part.PartData.Groups.Length; j++)
					{
						RealVector3 v = part.PartData.Groups[j].BoundsMax;
						if (v.x > boundsMax.x)
							boundsMax.x = v.x;
						if (v.y > boundsMax.y)
							boundsMax.y = v.y;
						if (v.z > boundsMax.z)
							boundsMax.z = v.z;

						v = part.PartData.Groups[j].BoundsMin;
						if (v.x < boundsMin.x)
							boundsMin.x = v.x;
						if (v.y < boundsMin.y)
							boundsMin.y = v.y;
						if (v.z < boundsMin.z)
							boundsMin.z = v.z;
					}
					part.PartInfo.BoundMax = boundsMax;
					part.PartInfo.BoundMin = boundsMin;

					// -- end --


					part.PartData.IndexCount = indexOffset;
					part.PartData.Indices = new ushort[indexOffset];

					int vertexOffset = 0;
					indexOffset = 0;
					for (int j=0; j<subMeshList.Length; j++)
					{
						for (int k=0; k<subMeshList[j].IndexList.Count; k++) 
						{
							part.PartData.Indices[indexOffset++] = (ushort)((ushort)subMeshList[j].IndexList[k] + vertexOffset);
						}
						vertexOffset += subMeshList[j].VertexList.Count;
					}

					part.PartData.TriangleCount = indexOffset / 3;
					part.PartData.Unk1 = 0x12;
					part.PartData.VBCount = 1;
					part.PartData.Vertices = new RealVertex[vertexOffset];

					vertexOffset = 0;
					for (int j=0; j<subMeshList.Length; j++)
					{
						for (int k=0; k<subMeshList[j].VertexList.Count; k++) 
						{
							part.PartData.Vertices[vertexOffset++] = (RealVertex)subMeshList[j].VertexList[k];
						}
					}

					geom.AddPart(part);

				}

				Compiler.VerboseOutput(" + Complete.");	

			}
			
			if (mountPointObjects.Count > 0)
			{
				Compiler.VerboseOutput("Merging mount points into base parts...");
				if (basePartObjects.Count == 0) {
					Compiler.WarningOutput("Mount points provided without any base parts! Ignoring mount points.");
				} 
				else
				{
					RealMountPoint[] mountPoints = new RealMountPoint[mountPointObjects.Count];
					mountPointObjects.CopyTo(mountPoints);
					foreach(RealGeometryPart part in basePartObjects)
					{
						part.PartInfo.MountPoints = mountPoints;
					}
				}
				Compiler.VerboseOutput(" + Complete.");	
			}

			Compiler.VerboseOutput("Collecting part cross links...");
			LoadCrossLinks(geom);

			geom.GeometryInfo.PartCount = geom.PartCount;
			geom.GeometryInfo.Unk1 = 0x1D;
			geom.GeometryInfo.Unk2 = 0x80;
			geom.GeometryInfo.ClassType = new FixedLenString("DEFAULT", 0x20);
			geom.GeometryInfo.RelFilePath = new FixedLenString("GEOMETRY.BIN", 0x38);

			Compiler.VerboseOutput("Data successfully collected.");
				
			return geom;
		}


		private MatTexStringPair GetMatTexPair(string mat)
		{
			MatTexStringPair pair = new MatTexStringPair();
			if (materialHT.ContainsKey(mat))
				mat = (string)materialHT[mat];
			string[] split = mat.Split('/');
			if (split.Length < 2)
			{
				Compiler.WarningOutput(string.Format("Material {0} has invalid name.", mat));
				pair.Material = "DEFAULT";
				pair.Texture = "DEFAULT";
			} 
			else
			{
				pair.Material = split[0];
				pair.Texture = split[1];					
			}
			return pair;
		}

		public RealGeometryFile Collect(AseFile aseFile)
		{
			RealGeometryFile geom = new RealGeometryFile();
			Hashtable MatTex = new Hashtable();

			LoadMaterialHT();

			for(int i=0; i<aseFile.MaterialList.Count; i++)
			{
				MatTex.Add(i.ToString(), GetMatTexPair(aseFile.MaterialList[i].Name));
				if (aseFile.MaterialList[i].HasSubMaterials)
				{
					for(int j=0; j<aseFile.MaterialList[i].SubMaterialCount; j++)
					{
						MatTex.Add(string.Format("{0}/{1}", i, j), GetMatTexPair(aseFile.MaterialList[i][j].Name));
					}
				} 
			}

			ArrayList mountPointObjects = new ArrayList();
			//ArrayList transparentObjects = new ArrayList(new string[] {"base_a","kit00_front_window_a","kit00_left_headlight_a"});
			ArrayList transparentObjects = new ArrayList();
			int diffuse;

			ArrayList basePartObjects = new ArrayList();

			for (int i=0; i<aseFile.ObjectCount; i++)
			{
				Compiler.VerboseOutput(string.Format("Compiling object {0}: {1}", i+1, aseFile[i].Name));
				
				if (aseFile[i].Name.StartsWith("#"))
				{
					string mountName;
					uint mountHash;
					mountName = aseFile[i].Name.Substring(1).ToUpper();
					if (mountName.IndexOf("[") > -1)
					{
						mountName = mountName.Substring(0, mountName.IndexOf("["));
					}
					if (mountName.StartsWith("0x"))
						mountHash = uint.Parse(mountName.Substring(2), NumberStyles.HexNumber);
					else
						mountHash = RealHash(mountName);
					Compiler.VerboseOutput(string.Format(" + Mount Point Name: {0}", mountName));
					Compiler.VerboseOutput(string.Format(" + Compiled Hash: 0x{0:x}", mountHash));
					RealMountPoint mp = new RealMountPoint();
					mp.Hash = mountHash;
					mp.Transform = new RealMatrix();
					mp.Transform.m = new float[16];
					float[] transform = aseFile[i].Transform.Matrix;
					mp.Transform.m[0] = 1.0f;
					mp.Transform.m[5] = 1.0f;
					mp.Transform.m[10] = 1.0f;
					mp.Transform.m[15] = 1.0f;
					mp.Transform.m[12] = -transform[13]; //y
					mp.Transform.m[13] = transform[12]; //x
					mp.Transform.m[14] = transform[14]; //z

					mountPointObjects.Add(mp);
				} 
				else
				{
					Hashtable subMeshes = new Hashtable();
					UniqueList textures = new UniqueList();
					UniqueList materials = new UniqueList();

					if (transparentObjects.Contains(aseFile[i].Name))
						diffuse = 0x7FFFFFFF;
					else
						diffuse = -1;

					for(int j=0; j<aseFile[i].Mesh.FaceList.Count; j++) 
					{
						SubMesh subMesh;
						AseFace face = aseFile[i].Mesh.FaceList[j];

						string matId = aseFile[i].MaterialReference.ToString();
						if (aseFile.MaterialList[aseFile[i].MaterialReference].HasSubMaterials)
						{
							matId += string.Format("/{0}", face.MaterialID);
						}

						MatTexPair pair = new MatTexPair();
						MatTexStringPair strPair = (MatTexStringPair)MatTex[matId];
						pair.Material = materials.Add(strPair.Material);
						pair.Texture = textures.Add(strPair.Texture);

						if (subMeshes.ContainsKey(pair))
							subMesh = subMeshes[pair] as SubMesh;
						else
						{
							subMesh = new SubMesh();
							subMesh.TextureId = pair.Texture;
							subMesh.MaterialId = pair.Material;
							subMeshes.Add(pair, subMesh);
						}
					
						
						AseVertex v1 = aseFile[i].Mesh.VertexList[face.A];
						AseVertex v2 = aseFile[i].Mesh.VertexList[face.B];
						AseVertex v3 = aseFile[i].Mesh.VertexList[face.C];
						AseVertex nv1 = face.NormalA;
						AseVertex nv2 = face.NormalB;
						AseVertex nv3 = face.NormalC;
						AseVertex tv1 = aseFile[i].Mesh.TextureVertexList[face.TextureA];
						AseVertex tv2 = aseFile[i].Mesh.TextureVertexList[face.TextureB];
						AseVertex tv3 = aseFile[i].Mesh.TextureVertexList[face.TextureC];
					
						RealVertex rv;

						// rv.Z controls UP/DOWN from front view
						// rv.Y controls LEFT/RIGHT from front view
						// rv.X controls IN/OUT from front view

						// v1
						rv = new RealVertex();
						rv.Initialize(true, 0);
					
						rv.Position.x = -v1.Y;
						rv.Position.y = v1.X;
						rv.Position.z = v1.Z;
						rv.Normal.x = -nv1.Y;
						rv.Normal.y = nv1.X;
						rv.Normal.z = nv1.Z;

						rv.Diffuse = diffuse;
						rv.UV.u = tv1.U;
						rv.UV.v = tv1.V;

						subMesh.IndexList.Add((ushort)subMesh.VertexList.Add(rv));

						// v2
						rv = new RealVertex();
						rv.Initialize(true, 0);
					
						rv.Position.x = -v2.Y;
						rv.Position.y = v2.X;
						rv.Position.z = v2.Z;
						rv.Normal.x = -nv2.Y;
						rv.Normal.y = nv2.X;
						rv.Normal.z = nv2.Z;

						rv.Diffuse = diffuse;
						rv.UV.u = tv2.U;
						rv.UV.v = tv2.V;

						subMesh.IndexList.Add((ushort)subMesh.VertexList.Add(rv));

						// v3
						rv = new RealVertex();
						rv.Initialize(true, 0);
					
						rv.Position.x = -v3.Y;
						rv.Position.y = v3.X;
						rv.Position.z = v3.Z;
						rv.Normal.x = -nv3.Y;
						rv.Normal.y = nv3.X;
						rv.Normal.z = nv3.Z;

						rv.Diffuse = diffuse;
						rv.UV.u = tv3.U;
						rv.UV.v = tv3.V;

						subMesh.IndexList.Add((ushort)subMesh.VertexList.Add(rv));

					}
			
					Compiler.VerboseOutput(string.Format(" + Compiled into {0} submeshes", subMeshes.Count));
					SubMesh[] subMeshList = new SubMesh[subMeshes.Count];
					subMeshes.Values.CopyTo(subMeshList, 0);
					for (int j=0; j<subMeshes.Count; j++)
					{
						SubMesh subMesh = subMeshList[j];

						Compiler.VerboseOutput(string.Format(" + Submesh {0}:", j+1));
						Compiler.VerboseOutput(string.Format("    + Material:  {0}", materials[subMesh.MaterialId]));
						Compiler.VerboseOutput(string.Format("    + Texture:   {0}", textures[subMesh.TextureId]));
						Compiler.VerboseOutput(string.Format("    + Vertices:  {0}", subMesh.VertexList.Count));
						Compiler.VerboseOutput(string.Format("    + Triangles: {0}", subMesh.IndexList.Count/3));
					}

					Compiler.VerboseOutput(string.Format("Creating part data for binary object file..."));

					RealGeometryPart part = new RealGeometryPart();

					//----- part info ----------

					RealVector4 boundsMin;
					RealVector4 boundsMax;

					if (aseFile[i].Name.ToUpper().StartsWith("BASE_"))
						basePartObjects.Add(part);

					string resolvedName = ResolveRealNameForce(aseFile[i].Name);

					part.PartInfo.Hash = RealHash(resolvedName);
					part.PartInfo.PartName = new FixedLenString(resolvedName);
					part.PartInfo.ShaderCount = (byte)materials.Count;
					part.PartInfo.Shaders = new uint[materials.Count];
					for (int j=0; j<materials.Count; j++)
					{
						string matName = materials[j] as string;
						if (matName.StartsWith("0x"))
							part.PartInfo.Shaders[j] = uint.Parse(matName.Substring(2), NumberStyles.HexNumber);
						else
							part.PartInfo.Shaders[j] = RealHash(matName);
					}

					part.PartInfo.TextureCount = (byte)textures.Count;
					part.PartInfo.Textures = new uint[textures.Count];
					for (int j=0; j<textures.Count; j++)
					{
						string texName = textures[j] as string;
						if (texName.StartsWith("0x"))
							part.PartInfo.Textures[j] = uint.Parse(texName.Substring(2), NumberStyles.HexNumber);
						else
							part.PartInfo.Textures[j] = RealHash(ResolveRealName(texName));
					}

					part.PartInfo.Transform.m = aseFile[i].Transform.Matrix;
					/*
					part.PartInfo.Transform.m = new float[16];
					part.PartInfo.Transform.m[0] = 1.0f;
					part.PartInfo.Transform.m[5] = 1.0f;
					part.PartInfo.Transform.m[10] = 1.0f;
					part.PartInfo.Transform.m[15] = 1.0f;
					*/

					part.PartInfo.TriangleCount = aseFile[i].Mesh.FaceList.Count;

					part.PartInfo.Unk1 = 0x00400018;
					part.PartInfo.Unk2 = 0x000EA550;
					part.PartInfo.Unk3 = 0x000EA550;

					part.PartInfo.Unk4_MW = 0;
					part.PartInfo.Unk5_MW = 1;
					part.PartInfo.Unk6_MW = part.PartInfo.TriangleCount;

					//----- part data ----------

					part.PartData.Flags = 0x000080 + /* 0x000100 */ + 0x004000 /* + 0x010000*/;
				
					part.PartData.GroupCount = subMeshList.Length;
					part.PartData.Groups = new RealShadingGroup[subMeshList.Length];

					int indexOffset = 0;
					for (int j=0; j<subMeshList.Length; j++)
					{
						SubMesh subMesh = subMeshList[j];
						part.PartData.Groups[j] = new RealShadingGroup();

						boundsMin = new RealVector4();
						boundsMax = new RealVector4();
						if (subMesh.VertexList.Count > 0)
						{
							RealVector3 v = ((RealVertex)subMesh.VertexList[0]).Position;
							boundsMin.x = v.x;
							boundsMin.y = v.y;
							boundsMin.z = v.z;
							boundsMax = boundsMin;
						}
						for(int k=1; k<subMesh.VertexList.Count; k++)
						{
							RealVector3 v = ((RealVertex)subMesh.VertexList[k]).Position;
							if (v.x > boundsMax.x)
								boundsMax.x = v.x;
							if (v.y > boundsMax.y)
								boundsMax.y = v.y;
							if (v.z > boundsMax.z)
								boundsMax.z = v.z;
							if (v.x < boundsMin.x)
								boundsMin.x = v.x;
							if (v.y < boundsMin.y)
								boundsMin.y = v.y;
							if (v.z < boundsMin.z)
								boundsMin.z = v.z;
						}
						part.PartData.Groups[j].BoundsMax = new RealVector3(boundsMax.x, boundsMax.y, boundsMax.z);
						part.PartData.Groups[j].BoundsMin = new RealVector3(boundsMin.x, boundsMin.y, boundsMin.z);

						part.PartData.Groups[j].Length = subMesh.IndexList.Count;
						part.PartData.Groups[j].TextureIndex0 = (byte)subMesh.TextureId;
						part.PartData.Groups[j].TextureIndex1 = (byte)subMesh.TextureId;
						part.PartData.Groups[j].TextureIndex2 = (byte)subMesh.TextureId;
						part.PartData.Groups[j].TextureIndex3 = (byte)subMesh.TextureId;
						part.PartData.Groups[j].TextureIndex4 = (byte)subMesh.TextureId;
						part.PartData.Groups[j].ShaderIndex0 = (byte)subMesh.MaterialId;

						part.PartData.Groups[j].Unk1 = 0x4;
						part.PartData.Groups[j].Flags = part.PartData.Flags;

						part.PartData.Groups[j].VertexCount = subMesh.VertexList.Count;
						part.PartData.Groups[j].TriangleCount = subMesh.IndexList.Count / 3;

						part.PartData.Groups[j].Offset = indexOffset;

						indexOffset += subMesh.IndexList.Count;
					
					}

					// -- bit of partinfo here -- caclulate bounds
					boundsMin = new RealVector4();
					boundsMax = new RealVector4();
					if (part.PartData.Groups.Length > 0)
					{
						RealVector3 v = part.PartData.Groups[0].BoundsMin;
						boundsMin.x = v.x;
						boundsMin.y = v.y;
						boundsMin.z = v.z;
						v = part.PartData.Groups[0].BoundsMax;
						boundsMax.x = v.x;
						boundsMax.y = v.y;
						boundsMax.z = v.z;
					}
					for (int j=1; j<part.PartData.Groups.Length; j++)
					{
						RealVector3 v = part.PartData.Groups[j].BoundsMax;
						if (v.x > boundsMax.x)
							boundsMax.x = v.x;
						if (v.y > boundsMax.y)
							boundsMax.y = v.y;
						if (v.z > boundsMax.z)
							boundsMax.z = v.z;

						v = part.PartData.Groups[j].BoundsMin;
						if (v.x < boundsMin.x)
							boundsMin.x = v.x;
						if (v.y < boundsMin.y)
							boundsMin.y = v.y;
						if (v.z < boundsMin.z)
							boundsMin.z = v.z;
					}
					part.PartInfo.BoundMax = boundsMax;
					part.PartInfo.BoundMin = boundsMin;

					// -- end --


					part.PartData.IndexCount = indexOffset;
					part.PartData.Indices = new ushort[indexOffset];

					int vertexOffset = 0;
					indexOffset = 0;
					for (int j=0; j<subMeshList.Length; j++)
					{
						for (int k=0; k<subMeshList[j].IndexList.Count; k++) 
						{
							part.PartData.Indices[indexOffset++] = (ushort)((ushort)subMeshList[j].IndexList[k] + vertexOffset);
						}
						vertexOffset += subMeshList[j].VertexList.Count;
					}

					part.PartData.TriangleCount = indexOffset / 3;
					part.PartData.Unk1 = 0x12;
					part.PartData.VBCount = 1;
					part.PartData.Vertices = new RealVertex[vertexOffset];

					vertexOffset = 0;
					for (int j=0; j<subMeshList.Length; j++)
					{
						for (int k=0; k<subMeshList[j].VertexList.Count; k++) 
						{
							part.PartData.Vertices[vertexOffset++] = (RealVertex)subMeshList[j].VertexList[k];
						}
					}

					geom.AddPart(part);

				}

				Compiler.VerboseOutput(" + Complete.");	

			}
			
			if (mountPointObjects.Count > 0)
			{
				Compiler.VerboseOutput("Merging mount points into base parts...");
				if (basePartObjects.Count == 0) 
				{
					Compiler.WarningOutput("Mount points provided without any base parts! Ignoring mount points.");
				} 
				else
				{
					RealMountPoint[] mountPoints = new RealMountPoint[mountPointObjects.Count];
					mountPointObjects.CopyTo(mountPoints);
					foreach(RealGeometryPart part in basePartObjects)
					{
						part.PartInfo.MountPoints = mountPoints;
					}
				}
				Compiler.VerboseOutput(" + Complete.");	
			}

			Compiler.VerboseOutput("Collecting part cross links...");
			LoadCrossLinks(geom);

			geom.GeometryInfo.PartCount = geom.PartCount;
			geom.GeometryInfo.Unk1 = 0x1D;
			geom.GeometryInfo.Unk2 = 0x80;
			geom.GeometryInfo.ClassType = new FixedLenString("DEFAULT", 0x20);
			geom.GeometryInfo.RelFilePath = new FixedLenString("GEOMETRY.BIN", 0x38);

			Compiler.VerboseOutput("Data successfully collected.");
				
			return geom;
		}

	
	}
}
