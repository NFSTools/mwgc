using System;
using System.Collections;
using System.IO;
using System.Reflection;

namespace mwgc.AseLib
{

	// Helpers
	public class AseReader
	{
		private TextReader _reader;
		private string _line;
		private string _nodeName;
		private string _nodeData;
		private bool _nodeParentStart;
		private bool _nodeParentEnd;

		public TextReader Reader
		{
			 get { return _reader; }
		}
			
		public string Line
		{
			get { return _line; }
		}

		public string NodeName
		{
			get { return _nodeName; }
		}

		public string NodeData
		{
			get { return _nodeData; }
		}

		public bool NodeParentStart
		{
			get { return _nodeParentStart; }
		}

		public bool NodeParentEnd
		{
			get { return _nodeParentEnd; }
		}

		public AseReader(TextReader reader)
		{
			_reader = reader;
		}

		public bool EndOfFile
		{
			get { return (_line == null); }
		}
		
		public void ReadNextLine()
		{
			while(true)
			{
				string line = _reader.ReadLine();
				if (line == null)
				{
					_line = null;
				} 
				else
				{
					_line = line.Trim();
					if (_line == "}")
					{
						_nodeName = null;
						_nodeData = null;
						_nodeParentStart = false;
						_nodeParentEnd = true;
					} 
					else
					{
						if (!_line.StartsWith("*"))
							continue;

						int endNodeNameSpace = _line.IndexOf(' ');
						int endNodeNameTab = _line.IndexOf('\t');
						int endNodeName = endNodeNameSpace;
						if (endNodeName == -1)
						{
							endNodeName = endNodeNameTab;
							if (endNodeName == -1)
								endNodeName = _line.Length - 1;
						} 
						else
						{
							if (endNodeNameTab != -1)
								endNodeName = (endNodeNameSpace<endNodeNameTab)?endNodeNameSpace:endNodeNameTab;
						}
							
						_nodeName = _line.Substring(1, endNodeName-1);
						_nodeData = _line.Substring(endNodeName).Trim();
						if (_nodeData.StartsWith("\"") && _nodeData.EndsWith("\""))
						{
							_nodeData = _nodeData.Substring(1, _nodeData.Length-2);
						}
						_nodeParentStart = _line.EndsWith("{");

						if (_nodeParentStart)
						{
							_nodeData = _nodeData.Substring(0, _nodeData.Length - 1).Trim();
						}

						_nodeParentEnd = false;						
					}
				}

				return;				
			}
		}
	}

	public class AseStringTokenizer
	{
		private Queue _tokens;
		
		public AseStringTokenizer(string str) : this(str, ' ', '\t')
		{
			
		}

		public AseStringTokenizer(string str, params char[] split) {
			string[] elements = str.Split(split);
			_tokens = new Queue();
			foreach(string element in elements)
			{
				if (element != "")
					_tokens.Enqueue(element);
			}
		}

		public string Peek()
		{
			return _tokens.Peek() as string;
		}

		public string GetNext()
		{
			return _tokens.Dequeue() as string;
		}

		public bool HasMore()
		{
			return (_tokens.Count > 0);
		}
	}


	// Core Base Classes
	public abstract class AseNode
	{
		
		protected void TraverseUnhandledNodes(AseReader reader)
		{
			reader.ReadNextLine();
			while(!reader.NodeParentEnd)
			{
				if (reader.NodeParentStart)
					TraverseUnhandledNodes(reader);
				reader.ReadNextLine();
			}
		}

		public virtual void ProcessNode(AseReader reader, AseNode parentNode)
		{
			ProcessNodePre(reader, parentNode);
			reader.ReadNextLine();
			while(!(reader.EndOfFile || reader.NodeParentEnd))
			{
				if (reader.NodeParentStart)
				{
					TraverseUnhandledNodes(reader);
				} 
				else
				{
					ProcessInnerNode(reader, parentNode);	
				}
				reader.ReadNextLine();
			}
			ProcessNodePost(reader, parentNode);
		}

		protected virtual void ProcessNodePre(AseReader reader, AseNode parentNode)
		{
			// do nothing
		}

		protected virtual void ProcessNodePost(AseReader reader, AseNode parentNode)
		{
			// do nothing
		}

		protected virtual void ProcessInnerNode(AseReader reader, AseNode parentNode)
		{
			// do nothing
		}
	}

	public abstract class AseExtendedNode : AseNode
	{
		protected Hashtable _nodeParsers;
		
		public AseExtendedNode()
		{
			_nodeParsers = new Hashtable();
		}
		
		protected void AddNodeParser(string nodeName, Type parser)
		{
			_nodeParsers.Add(nodeName, parser);
		}

		protected void AddNodeParser(string nodeName, AseNode parser)
		{
			_nodeParsers.Add(nodeName, parser);
		}
		
		protected AseNode GetParser(string nodeName)
		{
			Object parser = _nodeParsers[nodeName];
			if (parser == null)
				return null;
			if (parser is Type)
			{
				Type type = parser as Type;
				ConstructorInfo ci = type.GetConstructor(Type.EmptyTypes);
				AseNode node = ci.Invoke(null) as AseNode;
				return node;				
			} 
			else
			{
				return parser as AseNode;
			}
		}
	
		public override void ProcessNode(AseReader reader, AseNode parentNode)
		{
            ProcessNodePre(reader, parentNode);
			reader.ReadNextLine();
			while(!(reader.EndOfFile || reader.NodeParentEnd))
			{
				AseNode node = GetParser(reader.NodeName);
				if (node == null)
				{
					if (reader.NodeParentStart)
					{
						TraverseUnhandledNodes(reader);
					} 
					else
					{
						ProcessInnerNode(reader, parentNode);	
					}
				} 
				else
				{
					node.ProcessNode(reader, this);
				}
				reader.ReadNextLine();
			}
			ProcessNodePost(reader, parentNode);
		}

	}


	// Geometry
	public struct AseVertex
	{
		private float _x, _y, _z;
		public AseVertex(float x, float y, float z)
		{
			_x = x;
			_y = y;
			_z = z;
		}
		public float X
		{
			get { return _x; }
			set { _x = value; }
		}
		public float Y
		{
			get { return _y; }
			set { _y = value; }
		}
		public float Z
		{
			get { return _z; }
			set { _z = value;}
		}
		public float U
		{
			get { return _x; }
			set { _x = value; }
		}
		public float V
		{
			get { return _y; }
			set { _y = value; }
		}
		public float W
		{
			get { return _z; }
			set { _z = value;}
		}
	}

	public struct AseFace
	{
		private int _a, _b, _c;				// position vertex indices
		private bool _ab, _bc, _ca;			// the visible edges
		private int[] _smoothing;			// smoothing groups
		private int _materialId;			// material id
		private int _tA, _tB, _tC;			// texture vertex indices
		private AseVertex _faceNormal;		// face normal
		private AseVertex _aN, _bN, _cN;	// vertex normals

		public int A
		{
			get { return _a; }
			set { _a = value; }
		}
		public int B
		{
			get { return _b; }
			set { _b = value; }
		}
		public int C
		{
			get { return _c; }
			set { _c = value; }
		}
		public AseVertex NormalA
		{
			get { return _aN; }
			set { _aN = value; }
		}
		public AseVertex NormalB
		{
			get { return _bN; }
			set { _bN = value; }
		}
		public AseVertex NormalC
		{
			get { return _cN; }
			set { _cN = value; }
		}
		public AseVertex NormalFace
		{
			get { return _faceNormal; }
			set { _faceNormal = value; }
		}
		public int TextureA
		{
			get { return _tA; }
			set { _tA = value; }
		}
		public int TextureB
		{
			get { return _tB; }
			set { _tB = value; }
		}
		public int TextureC
		{
			get { return _tC; }
			set { _tC = value; }
		}
		public bool EdgeAB
		{
			get { return _ab; }
			set { _ab = value; }
		}
		public bool EdgeBC
		{
			get { return _bc; }
			set { _bc = value; }
		}
		public bool EdgeCA
		{
			get { return _ca; }
			set { _ca = value; }
		}
		public int SmoothingCount
		{
			get
			{
				if (_smoothing != null)
					return _smoothing.Length;
				else
					return 0;
			}
			set { _smoothing = new int[value]; }
		}
		public int this[int index]
		{
			get { return _smoothing[index]; }
			set { _smoothing[index] = value; }
		}
		public int MaterialID
		{
			get { return _materialId; }
			set { _materialId = value; }
		}
	}
	public class AseTransform : AseNode
	{
		protected float[] _matrix;

		public AseTransform() : base() {
			_matrix = new float[16];	
		}

		public float[] Matrix
		{
			get { return _matrix; }
		}

		public float this[int i, int j]
		{
			get { return _matrix[i*4+j]; }
			set { _matrix[i*4+j] = value; }
		}

		protected override void ProcessInnerNode(AseReader reader, AseNode parentNode)
		{
			AseStringTokenizer tokens;
			switch(reader.NodeName)
			{
				case "TM_ROW0":
					tokens = new AseStringTokenizer(reader.NodeData);
					this[0,0] = float.Parse(tokens.GetNext());
					this[0,1] = float.Parse(tokens.GetNext());
					this[0,2] = float.Parse(tokens.GetNext());
					this[0,3] = 0.0f;
					break;
				case "TM_ROW1":
					tokens = new AseStringTokenizer(reader.NodeData);
					this[1,0] = float.Parse(tokens.GetNext());
					this[1,1] = float.Parse(tokens.GetNext());
					this[1,2] = float.Parse(tokens.GetNext());
					this[1,3] = 0.0f;
					break;
				case "TM_ROW2":
					tokens = new AseStringTokenizer(reader.NodeData);
					this[2,0] = float.Parse(tokens.GetNext());
					this[2,1] = float.Parse(tokens.GetNext());
					this[2,2] = float.Parse(tokens.GetNext());
					this[2,3] = 0.0f;
					break;
				case "TM_ROW3":
					tokens = new AseStringTokenizer(reader.NodeData);
					this[3,0] = float.Parse(tokens.GetNext());
					this[3,1] = float.Parse(tokens.GetNext());
					this[3,2] = float.Parse(tokens.GetNext());
					this[3,3] = 1.0f;
					break;
			}
		}

	}
	
	public class AseFaceList : AseNode
	{
		protected AseFace[] _faces;

		public int Count
		{
			get { return _faces.Length; }
			set { _faces = new AseFace[value]; }
		}

		public AseFace this[int index]
		{
			get { return _faces[index]; }
			set { _faces[index] = value; }
		}

		protected override void ProcessInnerNode(AseReader reader, AseNode parentNode)
		{
			if (reader.NodeName == "MESH_FACE")
			{
				AseStringTokenizer tokens = new AseStringTokenizer(reader.NodeData);
				string indexStr = tokens.GetNext();
				if (indexStr.EndsWith(":"))
					indexStr = indexStr.Substring(0, indexStr.Length-1).Trim();
				int index = int.Parse(indexStr);

				for(int i=0; i<3; i++)
				{
					string type = tokens.GetNext();
					int value = int.Parse(tokens.GetNext());
					if (type == "A:")
						_faces[index].A = value;
					else if (type == "B:")
						_faces[index].B = value;
					else if (type == "C:")
						_faces[index].C = value;
				}

				for(int i=0; i<3; i++)
				{
					string type = tokens.GetNext();
					bool value = int.Parse(tokens.GetNext()) != 0;
					if (type == "AB:")
						_faces[index].EdgeAB = value;
					else if (type == "BC:")
						_faces[index].EdgeBC = value;
					else if (type == "CA:")
						_faces[index].EdgeCA = value;
				}

				while(tokens.HasMore())
				{
					string extended = tokens.GetNext();
					if (extended.StartsWith("*"))
					{
						if (extended == "*MESH_SMOOTHING")
						{							
							if (tokens.Peek().StartsWith("*"))
								continue;
							string[] meshSmooth = tokens.GetNext().Split(',');
							_faces[index].SmoothingCount = meshSmooth.Length;
							for(int i=0; i<meshSmooth.Length; i++)
								_faces[index][i] = int.Parse(meshSmooth[i]);
						} 
						else if (extended == "*MESH_MTLID")
						{
							if (tokens.Peek().StartsWith("*"))
								continue;
							_faces[index].MaterialID = int.Parse(tokens.GetNext());
						}
					}
				}
			}
		}

	}

	public class AseNormals	: AseNode
	{
		public static readonly AseNormals Instance = new AseNormals();
		protected override void ProcessInnerNode(AseReader reader, AseNode parentNode)
		{
			AseMesh mesh = parentNode as AseMesh;
			if (reader.NodeName == "MESH_FACENORMAL")
			{
				AseStringTokenizer tokens = new AseStringTokenizer(reader.NodeData);
				int index = int.Parse(tokens.GetNext());
				AseFace face = mesh.FaceList[index];
				face.NormalFace = new AseVertex(
						float.Parse(tokens.GetNext()), 
						float.Parse(tokens.GetNext()),
						float.Parse(tokens.GetNext()));
				for(int i=0; i<3; i++)
				{
					reader.ReadNextLine();
					tokens = new AseStringTokenizer(reader.NodeData);
					if (reader.NodeName == "MESH_VERTEXNORMAL")
					{
						int vIndex = int.Parse(tokens.GetNext());
						if (vIndex == mesh.FaceList[index].A)
						{
							face.NormalA = new AseVertex(
								float.Parse(tokens.GetNext()), 
								float.Parse(tokens.GetNext()),
								float.Parse(tokens.GetNext()));
						}
						else if (vIndex == mesh.FaceList[index].B)
						{
							face.NormalB = new AseVertex(
								float.Parse(tokens.GetNext()), 
								float.Parse(tokens.GetNext()),
								float.Parse(tokens.GetNext()));
						}
						else if (vIndex == mesh.FaceList[index].C)
						{
							face.NormalC = new AseVertex(
								float.Parse(tokens.GetNext()), 
								float.Parse(tokens.GetNext()),
								float.Parse(tokens.GetNext()));
						}
					}
					else
					{
						// we must have atleast 3 MESH_VERTEXNORMAL nodes
						i--;
					}
				}
				mesh.FaceList[index] = face;
			}
		}
	}

	public class AseTextureFaceList : AseNode
	{
		public static readonly AseTextureFaceList Instance = new AseTextureFaceList();
		protected override void ProcessInnerNode(AseReader reader, AseNode parentNode)
		{
            AseMesh mesh = parentNode as AseMesh;
			if (reader.NodeName == "MESH_TFACE")
			{
				AseStringTokenizer tokens = new AseStringTokenizer(reader.NodeData);
				int index = int.Parse(tokens.GetNext());
				AseFace face = mesh.FaceList[index];
				face.TextureA = int.Parse(tokens.GetNext());
				face.TextureB = int.Parse(tokens.GetNext());
				face.TextureC = int.Parse(tokens.GetNext());
				mesh.FaceList[index] = face;
			}
		}
	}

	public class AseTextureVertexList : AseBaseVertexList
	{
		protected override void ProcessInnerNode(AseReader reader, AseNode parentNode)
		{
			if (reader.NodeName == "MESH_TVERT")
			{
				AseStringTokenizer tokens = new AseStringTokenizer(reader.NodeData);
				int index = int.Parse(tokens.GetNext());
				_vertices[index].U = float.Parse(tokens.GetNext());
				_vertices[index].V = float.Parse(tokens.GetNext());
				_vertices[index].W = float.Parse(tokens.GetNext());
			}
		}
	}

	public class AseVertexList : AseBaseVertexList
	{
		protected override void ProcessInnerNode(AseReader reader, AseNode parentNode)
		{
			if (reader.NodeName == "MESH_VERTEX")
			{
				AseStringTokenizer tokens = new AseStringTokenizer(reader.NodeData);
				int index = int.Parse(tokens.GetNext());
				_vertices[index].X = float.Parse(tokens.GetNext());
				_vertices[index].Y = float.Parse(tokens.GetNext());
				_vertices[index].Z = float.Parse(tokens.GetNext());
			}
		}
	}

	public class AseBaseVertexList : AseNode
	{
		protected AseVertex[] _vertices;

		public int Count
		{
			get { return _vertices.Length; }
			set { _vertices = new AseVertex[value]; }
		}

		public AseVertex this[int index]
		{
			get { return _vertices[index]; }
			set { _vertices[index] = value; }
		}
		
	}

	public class AseMesh : AseExtendedNode
	{
		private AseVertexList _vertexList;
		private AseTextureVertexList _texVertexList;
		private AseFaceList _faceList;

		public AseVertexList VertexList
		{
			get { return _vertexList; }
			set { _vertexList = value; }
		}

		public AseTextureVertexList TextureVertexList
		{
			get { return _texVertexList; }
			set { _texVertexList = value; }
		}

		public AseFaceList FaceList
		{
			get { return _faceList; }
			set { _faceList = value; }
		}

		public AseMesh() : base()
		{
			AddNodeParser("MESH_VERTEX_LIST", _vertexList = new AseVertexList());
			AddNodeParser("MESH_FACE_LIST", _faceList = new AseFaceList());
			AddNodeParser("MESH_TVERTLIST", _texVertexList = new AseTextureVertexList());
			AddNodeParser("MESH_TFACELIST", AseTextureFaceList.Instance);
			AddNodeParser("MESH_NORMALS", AseNormals.Instance);
		}

		protected override void ProcessInnerNode(AseReader reader, AseNode parentNode)
		{
			switch(reader.NodeName)
			{
				case "TIMEVALUE":
					int timeValue = int.Parse(reader.NodeData);
					if (timeValue != 0)
						throw new AseException("Only models without any animation is supported.");
					break;
				case "MESH_NUMVERTEX":
					_vertexList.Count = int.Parse(reader.NodeData);
					break;
				case "MESH_NUMFACES":
					_faceList.Count = int.Parse(reader.NodeData);
					break;
				case "MESH_NUMTVERTEX":
					_texVertexList.Count = int.Parse(reader.NodeData);
					break;
				case "MESH_NUMTVFACES":
					// ignore this value under the assumption that all TFaces will
					// also have a Face struct associated with them.
					break;
			}
		}

	}

	public class AseGeometryObject : AseExtendedNode
	{
		private string _name;
		private int _materialRef;
		private AseTransform _transform;
		private AseMesh _mesh;

		public AseMesh Mesh
		{
			get { return _mesh; }
			set { _mesh = value; }
		}

		public AseTransform Transform
		{
			get { return _transform; }
			set { _transform = value; }
		}
		
		public string Name
		{
			get { return _name; }
		}

		public int MaterialReference
		{
			get { return _materialRef; }
		}

		public AseGeometryObject() : base()
		{
			AddNodeParser("NODE_TM", _transform = new AseTransform());
			AddNodeParser("MESH", _mesh = new AseMesh());
		}

		protected override void ProcessInnerNode(AseReader reader, AseNode parentNode)
		{
			switch(reader.NodeName)
			{
				case "NODE_NAME":
					_name = reader.NodeData;
					break;
				case "MATERIAL_REF":
					_materialRef = int.Parse(reader.NodeData);
					break;
			}
		}

		protected override void ProcessNodePre(AseReader reader, AseNode parentNode)
		{
			AseRoot root = parentNode as AseRoot;
			root.AddGeometryObject(this);
		}

	}


	// Materials
	public class AseSubMaterial : AseMaterial
	{

		protected override void ProcessNodePre(AseReader reader, AseNode parentNode)
		{
			if (parentNode is AseMaterial)
			{
				AseMaterial material = parentNode as AseMaterial;
				int index = int.Parse(reader.NodeData);
				material[index] = this;			
			}
		}

	}

	public class AseMaterial : AseExtendedNode
	{
		protected string _name;

		protected AseSubMaterial[] _subMaterials;
		
		public string Name
		{
			get { return _name; }
		}

		public AseSubMaterial this[int index]
		{
			get { return _subMaterials[index]; }
			set { _subMaterials[index] = value; }
		}

		public bool HasSubMaterials
		{
			get { return (_subMaterials != null); }
		}

		public int SubMaterialCount
		{
			get { return _subMaterials.Length; }
		}

		public AseMaterial() : base()
		{
			_subMaterials = null;
			AddNodeParser("SUBMATERIAL", typeof(AseSubMaterial));
		}

		protected override void ProcessInnerNode(AseReader reader, AseNode parentNode)
		{
			switch(reader.NodeName)
			{
				case "MATERIAL_NAME":
					_name = reader.NodeData;
					break;
				case "NUMSUBMTLS":
					if (parentNode is AseSubMaterial)
						throw new AseException("Internal Sanity Check: SubMaterial within SubMaterial!");
					_subMaterials = new AseSubMaterial[int.Parse(reader.NodeData)];
					break;
			}
		}

		protected override void ProcessNodePre(AseReader reader, AseNode parentNode)
		{
			if (parentNode is AseMaterialList)
			{
				AseMaterialList list = parentNode as AseMaterialList;
				int index = int.Parse(reader.NodeData);
				list[index] = this;			
			}
		}


	}

	public class AseMaterialList : AseExtendedNode
	{
		private AseMaterial[] _materials;

		public AseMaterial this[int index]
		{
			get { return _materials[index]; }
			set { _materials[index] = value; }
		}

		public int Count
		{
			get { return _materials.Length; }
		}

		public AseMaterialList() : base()
		{
			AddNodeParser("MATERIAL", typeof(AseMaterial));
		}

		protected override void ProcessInnerNode(AseReader reader, AseNode parentNode)
		{
			switch(reader.NodeName)
			{
				case "MATERIAL_COUNT":
					_materials = new AseMaterial[int.Parse(reader.NodeData)];
					break;
			}
		}

		protected override void ProcessNodePre(AseReader reader, AseNode parentNode)
		{
			AseRoot root = parentNode as AseRoot;
			root.MaterialList = this;
		}

	}


	public class AseRoot : AseExtendedNode
	{
		private AseMaterialList _materialList;
		private ArrayList _geomObjects;

		public AseGeometryObject this[int index]
		{
			get { return _geomObjects[index] as AseGeometryObject; }
		}

		public int ObjectCount
		{
			get { return _geomObjects.Count; }
		}

		public void AddGeometryObject(AseGeometryObject obj)
		{
			_geomObjects.Add(obj);
		}

		public AseMaterialList MaterialList
		{
			get { return _materialList; }
			set { _materialList = value; }
		}

		public AseRoot() : base()
		{
			_geomObjects = new ArrayList();
			AddNodeParser("MATERIAL_LIST", _materialList = new AseMaterialList());
			AddNodeParser("GEOMOBJECT", typeof(AseGeometryObject));
		}

	}


	public class AseException : Exception
	{
		public AseException(string message) : base(message) {}
		public AseException(string message, Exception innerException) : base(message, innerException) {}
	}


	public class AseFile : AseRoot
	{

		public AseFile()
		{
			
		}

		public AseFile(string filename)
		{
			Open(filename);
		}

		public void Open(string filename)
		{
			StreamReader reader = new StreamReader(filename);
			AseReader aseReader = new AseReader(reader);
			aseReader.ReadNextLine();

			if (aseReader.NodeName != "3DSMAX_ASCIIEXPORT")
				throw new AseException("Not a valid ASE file.");
			
			int version = int.Parse(aseReader.NodeData);
			if (version != 200)
				throw new AseException("The version of the ASE file is not supported.");

			base.ProcessNode(aseReader, null);
			reader.Close();
		}

	}
}
