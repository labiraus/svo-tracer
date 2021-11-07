using SvoTracer.Domain.Models;
using SvoTracer.Domain.Serializers;
using System;
using System.IO;

namespace SvoTracer.Domain
{
	public class TreeManager : ITreeManager
	{
		private readonly string _path;

		public TreeManager(string path)
		{
			_path = path;
		}

		public void DeleteTree(string fileName)
		{
			File.Delete($"{_path}\\{fileName}.oct");

		}

		public bool TreeExists(string fileName)
		{
			return File.Exists($"{_path}\\{fileName}.oct");
		}

		public Octree LoadTree(string fileName)
		{
			using FileStream fs = new FileStream($"{_path}\\{fileName}.oct", FileMode.Open);
			using BinaryReader br = new BinaryReader(fs);
			var tree = OctreeSerializer.Deserialize(br);

			br.Close();
			fs.Close();
			return tree;
		}

		public void SaveTree(string fileName, Octree tree)
		{
			try
			{
				if (!Directory.Exists(_path))
					Directory.CreateDirectory(_path);
				using FileStream fs = File.Create($"{_path}\\{fileName}.oct", 2048, FileOptions.None);
				using BinaryWriter bw = new BinaryWriter(fs);
				tree.Serialize(bw);

				bw.Close();
				fs.Close();
			}
			catch (Exception e)
			{
				Console.Write(e.Message);
				Console.ReadKey(true);
			}
		}

	}
}
