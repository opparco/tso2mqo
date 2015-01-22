using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml;

namespace Tso2MqoGui
{
    public enum MqoBoneMode
    {
        None,
        RokDeBone,
        Mikoto,
    }

    public class Pair<T, U>
    {
        public T First;
        public U Second;

        public Pair()
        {
        }

        public Pair(T first, U second)
        {
            First = first;
            Second = second;
        }
    }

    public class MqoWriter : IDisposable
    {
        public TextWriter tw;
        public string OutPath;
        public string OutFile;
        public MqoBoneMode BoneMode = MqoBoneMode.None;

        public MqoWriter(string file)
        {
            FileStream fs = File.OpenWrite(file);
            fs.SetLength(0);
            tw = new StreamWriter(fs, Encoding.Default);
            OutFile = file;
            OutPath = Path.GetDirectoryName(file);
        }

        void IDisposable.Dispose()
        {
            Close();
        }

        public void Close()
        {
            if (tw != null)
                tw.Close();
            tw = null;
        }

        string GetTextureFileName(TSOTex tex)
        {
            string filename = Path.GetFileName(tex.File.Trim('"'));
            if (filename == "")
                filename = "none";
            return filename;
        }

        string GetTexturePath(TSOTex tex)
        {
            return Path.Combine(OutPath, GetTextureFileName(tex));
        }

        public void CreateTextureFile(TSOTex tex)
        {
            string file = GetTexturePath(tex);
            byte[] data = tex.data;

            //TODO: .bmpのはずが.psdになってるものがある

            using (FileStream fs = File.OpenWrite(file))
            {
                BinaryWriter bw = new BinaryWriter(fs);

                switch (Path.GetExtension(file).ToUpper())
                {
                    case ".TGA":
                        bw.Write((byte)0);              // id
                        bw.Write((byte)0);              // colormap
                        bw.Write((byte)2);              // imagetype
                        bw.Write((byte)0);              // unknown0
                        bw.Write((byte)0);              // unknown1
                        bw.Write((byte)0);              // unknown2
                        bw.Write((byte)0);              // unknown3
                        bw.Write((byte)0);              // unknown4
                        bw.Write((short)0);             // width
                        bw.Write((short)0);             // height
                        bw.Write((short)tex.Width);     // width
                        bw.Write((short)tex.Height);    // height
                        bw.Write((byte)(tex.depth * 8));// depth
                        bw.Write((byte)0);              // depth
                        break;

                    default:
                        bw.Write((byte)'B');
                        bw.Write((byte)'M');
                        bw.Write((int)(54 + data.Length));
                        bw.Write((int)0);
                        bw.Write((int)54);
                        bw.Write((int)40);
                        bw.Write((int)tex.Width);
                        bw.Write((int)tex.Height);
                        bw.Write((short)1);
                        bw.Write((short)(tex.Depth * 8));
                        bw.Write((int)0);
                        bw.Write((int)data.Length);
                        bw.Write((int)0);
                        bw.Write((int)0);
                        bw.Write((int)0);
                        bw.Write((int)0);
                        break;
                }

                bw.Write(data, 0, data.Length);
                bw.Flush();
            }
        }

        public void Write(TSOFile tso)
        {
            // ボーンを出す
            bool mqx_enabled = BoneMode == MqoBoneMode.RokDeBone;

            tw.WriteLine("Metasequoia Document");
            tw.WriteLine("Format Text Ver 1.0");
            tw.WriteLine("");
            if (mqx_enabled)
            {
                tw.WriteLine("IncludeXml \"{0}\"", Path.GetFileName(Path.ChangeExtension(OutFile, ".mqx")));
                tw.WriteLine("");
            }
            tw.WriteLine("Scene {");
            tw.WriteLine("\tpos -7.0446 4.1793 1541.1764");
            tw.WriteLine("\tlookat 11.8726 193.8590 0.4676");
            tw.WriteLine("\thead 0.8564");
            tw.WriteLine("\tpich 0.1708");
            tw.WriteLine("\tortho 0");
            tw.WriteLine("\tzoom2 31.8925");
            tw.WriteLine("\tamb 0.250 0.250 0.250");
            tw.WriteLine("}");

            foreach (TSOTex tex in tso.textures)
                CreateTextureFile(tex);

            tw.WriteLine("Material {0} {{", tso.materials.Length);

            foreach (TSOMaterial mat in tso.materials)
            {
                TSOTex tex = null;
                if (tso.texturemap.TryGetValue(mat.ColorTex, out tex))
                {
                    tw.WriteLine(
                        "\t\"{0}\" col(1.000 1.000 1.000 1.000) dif(0.800) amb(0.600) emi(0.000) spc(0.000) power(5.00) tex(\"{1}\")",
                        mat.name, GetTextureFileName(tex));
                }
                else
                {
                    tw.WriteLine(
                        "\t\"{0}\" col(1.000 1.000 1.000 1.000) dif(0.800) amb(0.600) emi(0.000) spc(0.000) power(5.00))",
                        mat.name);
                }
            }

            tw.WriteLine("}");

            tso.UpdateNodesWorld();

            MqoBone[] bones = new MqoBone[tso.nodes.Length];

            foreach (TSONode node in tso.nodes)
            {
                MqoBone bone = new MqoBone();
                bone.id = node.id;
                bone.name = node.ShortName;
                bone.tail = node.children.Count == 0;

                if (node.parent == null)
                {
                    bone.pid = -1;
                }
                else
                {
                    bone.pid = node.parent.id;
                    bones[bone.pid].cids.Add(bone.id);
                }

                //根本
                bone.q = node.world.Translation;
                //先端
                if (! bone.tail)
                    bone.p = node.children[0].world.Translation;
                else
                    bone.p = node.world.Translation;

                bones[node.id] = bone;
            }

            MqoObjectGen.uid_enabled = mqx_enabled;
            MqoObjectGen obj = new MqoObjectGen();

            ushort object_id = 0;
            foreach (TSOMesh mesh in tso.meshes)
            {
                obj.id = ++object_id;
                obj.name = mesh.Name;
                obj.Update(mesh);
                obj.Write(tw);
                obj.AddWeits(bones);
            }

            if (mqx_enabled)
                WriteMqxDeBone(bones, object_id /* eq numobjects */);

            tw.WriteLine("Eof");
        }

        void WriteMqxDeBone(MqoBone[] bones, int numobjects)
        {
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Indent = true;
            settings.IndentChars = new String(' ', 4);
            XmlWriter writer = XmlWriter.Create(Path.ChangeExtension(OutFile, ".mqx"), settings);
            writer.WriteStartElement("MetasequoiaDocument");
                writer.WriteElementString("IncludedBy", Path.GetFileName(OutFile));

            writer.WriteStartElement("Plugin.56A31D20.71F282AB");
                writer.WriteAttributeString("name", "Bone");
            writer.WriteStartElement("BoneSet");

            foreach (MqoBone bone in bones)
                bone.Write(writer);

            writer.WriteEndElement();//BoneSet

            for (int i = 0; i < numobjects; i++)
            {
                writer.WriteStartElement("Obj");
                writer.WriteAttributeString("id", (i+1).ToString());
                writer.WriteEndElement();
            }
            writer.WriteEndElement();//Plugin.56A31D20.71F282AB

            writer.WriteEndElement();//MetasequoiaDocument
            writer.Close();
        }

        void WriteRokDeBone(MqoBone[] bones)
        {
            tw.WriteLine("Object \"{0}\" {{", "Bone");
            tw.WriteLine("\tvisible {0}", 15);
            tw.WriteLine("\tlocking {0}", 0);
            tw.WriteLine("\tshading {0}", 1);
            tw.WriteLine("\tfacet {0}", 59.5);
            tw.WriteLine("\tcolor {0} {1} {2}", 1, 0, 0);
            tw.WriteLine("\tcolor_type {0}", 0);

            tw.WriteLine("\tvertex {0} {{", bones.Length);

            foreach (MqoBone bone in bones)
                tw.WriteLine("\t\t{0:F4} {1:F4} {2:F4}", bone.q.x, bone.q.y, bone.q.z);

            tw.WriteLine("\t}");

            //
            tw.WriteLine("\tface {0} {{", bones.Length);

            foreach (MqoBone bone in bones)
            {
                if (bone.pid == -1)
                    continue;

                //根元と先端を接続するedge
                if (! bone.tail)
                    tw.WriteLine(string.Format("\t\t2 V({0} {1})", bone.pid, bone.id));
            }

            tw.WriteLine("\t}");
            tw.WriteLine("}");
        }
    }

    public class MqoObjectGen
    {
        public static bool uid_enabled;

        public int id; //object_id
        public string name;
        VertexHeap<UVertex> vh = new VertexHeap<UVertex>();
        public List<MqoFace> faces;

        public int numvertices { get { return vh.Count; } }
        public List<UVertex> vertices { get { return vh.verts; } }
        public int numfaces { get { return faces.Count; } }

        public MqoObjectGen()
        {
            faces = new List<MqoFace>(2048);
        }

        public void Update(TSOMesh mesh)
        {
            vh.Clear();
            faces.Clear();

            foreach (TSOSubMesh sub_mesh in mesh.sub_meshes)
            {
                int cnt = 0;
                ushort a = 0, b = 0, c = 0;
                Vertex va = new Vertex(), vb = new Vertex(), vc = new Vertex();

                foreach (Vertex v in sub_mesh.vertices)
                {
                    ++cnt;
                    va = vb; a = b;
                    vb = vc; b = c;
                    vc = v; c = vh.Add(new UVertex(v.Pos, v.Wgt, v.Idx, v.Nrm));

                    if (cnt < 3) continue;
                    if (a == b || b == c || c == a) continue;

                    if ((cnt & 1) == 0)
                    {
                        MqoFace f = new MqoFace(a, b, c, (ushort)sub_mesh.spec,
                                new Point2(va.Tex.x, 1 - va.Tex.y),
                                new Point2(vb.Tex.x, 1 - vb.Tex.y),
                                new Point2(vc.Tex.x, 1 - vc.Tex.y));
                        faces.Add(f);
                    }
                    else
                    {
                        MqoFace f = new MqoFace(a, c, b, (ushort)sub_mesh.spec,
                                new Point2(va.Tex.x, 1 - va.Tex.y),
                                new Point2(vc.Tex.x, 1 - vc.Tex.y),
                                new Point2(vb.Tex.x, 1 - vb.Tex.y));
                        faces.Add(f);
                    }
                }
            }
        }

        public void Write(TextWriter tw)
        {
            tw.WriteLine("Object \"{0}\" {{", name);
            if (uid_enabled)
                tw.WriteLine("\tuid {0}", id);
            tw.WriteLine("\tvisible {0}", 15);
            tw.WriteLine("\tlocking {0}", 0);
            tw.WriteLine("\tshading {0}", 1);
            tw.WriteLine("\tfacet {0}", 59.5);
            tw.WriteLine("\tcolor {0:F3} {1:F3} {2:F3}", 0.898f, 0.498f, 0.698f);
            tw.WriteLine("\tcolor_type {0}", 0);

            //
            tw.WriteLine("\tvertex {0} {{", numvertices);

            foreach (UVertex v in vertices)
                v.Write(tw);

            tw.WriteLine("\t}");

            if (uid_enabled)
            {
                tw.WriteLine("\tvertexattr {");
                tw.WriteLine("\t\tuid {");

                ushort vertex_id = 0;
                foreach (UVertex v in vertices)
                    tw.WriteLine("\t\t\t{0}", ++vertex_id);

                tw.WriteLine("\t\t}");
                tw.WriteLine("\t}");
            }

            //
            tw.WriteLine("\tface {0} {{", numfaces);

            for (int i = 0, n = numfaces; i < n; i++)
                faces[i].Write(tw);
            tw.WriteLine("\t}");
            tw.WriteLine("}");
        }

        public unsafe void AddWeits(MqoBone[] bones)
        {
            ushort vertex_id = 0;
            foreach (UVertex v in vertices)
            {
                ++vertex_id;

                uint idx0 = v.Idx;
                byte* idx = (byte*)(&idx0);
                Point4 wgt0 = v.Wgt;
                float* wgt = (float*)(&wgt0);

                for (int k = 0; k < 4; ++k)
                    if (wgt[k] > float.Epsilon)
                    {
                        MqoWeit weit = new MqoWeit();
                        weit.object_id = id;
                        weit.vertex_id = vertex_id;
                        weit.weit = wgt[k];
                        bones[idx[k]].weits.Add(weit);
                    }
            }
        }
    }

    public class MqoBone
    {
        public int id;
        public string name;
        public bool tail;
        //なければ-1
        public int pid;
        public List<int> cids = new List<int>();

        //根本position
        public Point3 q;

        //先端position
        public Point3 p;

        public List<MqoWeit> weits;

        public MqoBone()
        {
            weits = new List<MqoWeit>(2048*3*4);
        }

        public void Write(XmlWriter writer)
        {
            writer.WriteStartElement("Bone");
            writer.WriteAttributeString("id", (id+1).ToString());

            writer.WriteAttributeString("rtX", q.X.ToString());
            writer.WriteAttributeString("rtY", q.Y.ToString());
            writer.WriteAttributeString("rtZ", q.Z.ToString());

            writer.WriteAttributeString("tpX", p.X.ToString());
            writer.WriteAttributeString("tpY", p.Y.ToString());
            writer.WriteAttributeString("tpZ", p.Z.ToString());

            writer.WriteAttributeString("rotB", "0.0");
            writer.WriteAttributeString("rotH", "0.0");
            writer.WriteAttributeString("rotP", "0.0");

            writer.WriteAttributeString("mvX", "0.0");
            writer.WriteAttributeString("mvY", "0.0");
            writer.WriteAttributeString("mvZ", "0.0");

            writer.WriteAttributeString("sc", "1.0");

            writer.WriteAttributeString("maxAngB", "90.0");
            writer.WriteAttributeString("maxAngH", "180.0");
            writer.WriteAttributeString("maxAngP", "180.0");

            writer.WriteAttributeString("minAngB", "-90.0");
            writer.WriteAttributeString("minAngH", "-180.0");
            writer.WriteAttributeString("minAngP", "-180.0");

            writer.WriteAttributeString("isDummy", tail ? "1" : "0");
            writer.WriteAttributeString("name", name);

            writer.WriteStartElement("P");
            writer.WriteAttributeString("id", (pid+1).ToString());
            writer.WriteEndElement();

            foreach (int cid in cids)
            {
                writer.WriteStartElement("C");
                writer.WriteAttributeString("id", (cid+1).ToString());
                writer.WriteEndElement();
            }
            foreach (MqoWeit weit in weits)
            {
                weit.Write(writer);
            }

            writer.WriteEndElement();
        }
    }

    public class MqoWeit
    {
        public int object_id;
        public int vertex_id;
        //public int bone_id;
        public float weit;

        public void Write(XmlWriter writer)
        {
            float weit_percent = weit * 100.0f;

            writer.WriteStartElement("W");
            writer.WriteAttributeString("oi", object_id.ToString());
            writer.WriteAttributeString("vi", vertex_id.ToString());
            writer.WriteAttributeString("w", weit_percent.ToString());
            writer.WriteEndElement();
        }
    }

    public class UVertex : IComparable<UVertex>
    {
        //public int id; //vertex_id
        public Point3 Pos;
        public Point4 Wgt;
        public UInt32 Idx;
        public Point3 Nrm;

        public UVertex()
        {
        }

        public UVertex(Point3 pos, Point4 wgt, UInt32 idx, Point3 nrm)
        {
            Pos = pos;
            Wgt = wgt;
            Idx = idx;
            Nrm = nrm;
        }

        public int CompareTo(UVertex o)
        {
            int cmp;
            cmp = Pos.CompareTo(o.Pos); if (cmp != 0) return cmp;
            cmp = Nrm.CompareTo(o.Nrm);
            return cmp;
        }

        public override int GetHashCode()
        {
            return Pos.GetHashCode() ^ Nrm.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (obj is UVertex)
            {
                UVertex o = (UVertex)obj;
                return Pos.Equals(o.Pos) && Nrm.Equals(o.Nrm);
            }
            return false;
        }

        public bool Equals(UVertex o)
        {
            if ((object)o == null)
            {
                return false;
            }

            return Pos.Equals(o.Pos) && Nrm.Equals(o.Nrm);
        }

        public void Write(TextWriter tw)
        {
            tw.WriteLine("\t\t{0:F4} {1:F4} {2:F4}", Pos.x, Pos.y, Pos.z);
        }
    }
}
