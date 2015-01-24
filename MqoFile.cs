using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Tso2MqoGui
{
    public class MqoFile
    {
        delegate bool SectionHandler(string[] tokens);

        static char[] param_delimiters = new char[] { ' ', '\t', '(', ')' };

        string file;
        StreamReader sr;
        MqoScene scene;
        List<MqoMaterial> materials;
        List<MqoObject> objects = new List<MqoObject>();
        MqoObject current;

        public MqoScene Scene { get { return scene; } }
        public List<MqoMaterial> Materials { get { return materials; } }
        public List<MqoObject> Objects { get { return objects; } }

        public void Load(string file)
        {
            using (FileStream fs = File.OpenRead(file))
            {
                this.file = file;
                sr = new StreamReader(fs, Encoding.Default);
                ReadAll();
            }
        }

        public void ReadAll()
        {
            DoRead(SectionRoot);
        }

        static string[] SplitString(string s)
        {
            List<string> tokens = new List<string>();
            StringBuilder sb = new StringBuilder(s.Length);
            bool str = false;
            bool escape = false;
            bool bracket = false;
            s = s.Trim(' ', '\t', '\r', '\n');

            foreach (char i in s)
            {
                if (escape)
                {
                    sb.Append(i);
                    escape = false;
                    continue;
                }


                switch (i)
                {
                    case '\\':
                        if (str) sb.Append(i);
                        else escape = true;
                        break;
                    case ' ':
                    case '\t':
                        if (bracket) { sb.Append(i); }
                        else if (str) { sb.Append(i); }
                        else if (sb.Length > 0) { tokens.Add(sb.ToString()); sb.Length = 0; }
                        break;
                    case '(':
                        sb.Append(i);
                        if (!str)
                            bracket = true;
                        break;
                    case ')':
                        sb.Append(i);
                        if (!str)
                            bracket = false;
                        break;
                    case '\"':
                        sb.Append(i);
                        str = !str;
                        break;
                    default:
                        sb.Append(i);
                        break;
                }
            }

            if (sb.Length > 0)
                tokens.Add(sb.ToString());

            return tokens.ToArray();
        }

        void DoRead(SectionHandler h)
        {
            for (int lineno = 1; ; ++lineno)
            {
                string line = sr.ReadLine();

                if (line == null)
                    break;

                line = line.Trim();
                string[] tokens = SplitString(line);

                try
                {
                    if (tokens.Length == 0)
                        continue;

                    if (!h(tokens))
                        break;
                }
                catch (Exception exception)
                {
                    throw new Exception(string.Format("File format error: {0} \"{1}\"", lineno, line), exception);
                }
            }
        }

        public void Error(string[] tokens)
        {
            throw new Exception(string.Format("File Format Error: \"{0}\"", string.Concat(tokens)));
        }

        bool SectionRoot(string[] tokens)
        {
            switch (tokens[0])
            {
                case "Metasequoia":
                    {
                        // Metasequoia Document
                        if (tokens[1] != "Document")
                            Error(tokens);
                    }
                    return true;
                case "Format":
                    {
                        // @since v2.2
                        // Format Text Ver 1.0
                        // @since v4.0
                        // Format Text Ver 1.1
                        if (tokens[1] != "Text")
                            Error(tokens);
                        if (tokens[2] != "Ver")
                            Error(tokens);
                        if (tokens[3] != "1.0" && tokens[3] != "1.1")
                            Error(tokens);
                    }
                    return true;
                case "Thumbnail":
                    {
                        // Thumbnail 128 128 24 rgb raw {
                        // ...
                        // }
                        if (tokens[6] != "{")
                            Error(tokens);

                        DoRead(SectionThumbnail);
                    }
                    return true;
                case "Scene":
                    {
                        if (tokens[1] != "{")
                            Error(tokens);

                        DoRead(SectionScene);
                    }
                    return true;
                case "Material":
                    {
                        if (tokens[2] != "{")
                            Error(tokens);

                        materials = new List<MqoMaterial>(int.Parse(tokens[1]));
                        DoRead(SectionMaterial);
                    }
                    return true;
                case "Object":
                    {
                        if (tokens[2] != "{")
                            Error(tokens);

                        current = new MqoObject(tokens[1].Trim('"'));
                        objects.Add(current);
                        DoRead(SectionObject);
                    }
                    return true;
                case "Eof":
                    return false;
                default:
                    return true;
            }
        }

        bool SectionThumbnail(string[] tokens)
        {
            switch (tokens[0])
            {
                case "}":
                    return false;
                default:
                    return true;
            }
        }

        bool SectionScene(string[] tokens)
        {
            scene = new MqoScene();

            switch (tokens[0])
            {
                case "pos": scene.pos = Point3.Parse(tokens, 1); return true;
                case "lookat": scene.lookat = Point3.Parse(tokens, 1); return true;
                case "head": scene.head = float.Parse(tokens[1]); return true;
                case "pich": scene.pich = float.Parse(tokens[1]); return true;
                case "ortho": scene.ortho = float.Parse(tokens[1]); return true;
                case "zoom2": scene.zoom2 = float.Parse(tokens[1]); return true;
                case "amb": scene.amb = Color3.Parse(tokens, 1); return true;
                case "dirlights":
                    {
                        // dirlights 1 {
                        // ...
                        // }
                        if (tokens[2] != "{")
                            Error(tokens);

                        DoRead(SectionDirlights);
                    }
                    return true;
                case "}":
                    return false;
                default:
                    return true;
            }
        }

        bool SectionDirlights(string[] tokens)
        {
            switch (tokens[0])
            {
                case "light":
                    {
                        // light {
                        // ...
                        // }
                        if (tokens[1] != "{")
                            Error(tokens);

                        DoRead(SectionLight);
                    }
                    break;
                case "}":
                    return false;
            }
            return true;
        }

        bool SectionLight(string[] tokens)
        {
            switch (tokens[0])
            {
                case "}":
                    return false;
            }
            return true;
        }

        static string[] SplitParam(string s)
        {
            return s.Split(param_delimiters, StringSplitOptions.RemoveEmptyEntries);
        }

        bool SectionMaterial(string[] tokens)
        {
            if (tokens[0] == "}")
                return false;

            StringBuilder sb = new StringBuilder();

            foreach (string i in tokens)
                sb.Append(' ').Append(i);

            string line = sb.ToString().Trim();
            MqoMaterial m = new MqoMaterial(tokens[0].Trim('"'));
            tokens = SplitString(line);
            materials.Add(m);

            for (int i = 1; i < tokens.Length; ++i)
            {
                string t = tokens[i];
                string t2 = t.ToLower();

                if (t2.StartsWith("shader(")) m.shader = int.Parse(SplitParam(t)[1]);
                else if (t2.StartsWith("col(")) m.col = Color3.Parse(SplitParam(t), 1);
                else if (t2.StartsWith("dif(")) m.dif = float.Parse(SplitParam(t)[1]);
                else if (t2.StartsWith("amb(")) m.amb = float.Parse(SplitParam(t)[1]);
                else if (t2.StartsWith("emi(")) m.emi = float.Parse(SplitParam(t)[1]);
                else if (t2.StartsWith("spc(")) m.spc = float.Parse(SplitParam(t)[1]);
                else if (t2.StartsWith("power(")) m.power = float.Parse(SplitParam(t)[1]);
                else if (t2.StartsWith("tex(")) m.tex = t.Substring(3).Trim('(', ')', '"');
            }

            return true;
        }

        bool SectionObject(string[] tokens)
        {
            switch (tokens[0])
            {
                case "visible": current.visible = int.Parse(tokens[1]); break;
                case "locking": current.locking = int.Parse(tokens[1]); break;
                case "shading": current.shading = int.Parse(tokens[1]); break;
                case "facet": current.facet = float.Parse(tokens[1]); break;
                case "color": current.color = Color3.Parse(tokens, 1); break;
                case "color_type": current.color_type = int.Parse(tokens[1]); break;
                case "vertex":
                    {
                        if (tokens[2] != "{")
                            Error(tokens);

                        current.vertices = new List<UVertex>(int.Parse(tokens[1]));
                        DoRead(SectionVertex);
                    }
                    break;
                case "vertexattr":
                    {
                        if (tokens[1] != "{")
                            Error(tokens);

                        DoRead(SectionVertexAttr);
                    }
                    break;
                case "face":
                    {
                        if (tokens[2] != "{")
                            Error(tokens);

                        current.faces = new List<MqoFace>(int.Parse(tokens[1]));
                        DoRead(SectionFace);
                    }
                    break;
                case "}":
                    return false;
            }
            return true;
        }

        bool SectionVertex(string[] tokens)
        {
            if (tokens[0] == "}")
                return false;

            UVertex v = new UVertex();
            v.Pos = Point3.Parse(tokens, 0);
            current.vertices.Add(v);

            return true;
        }

        bool SectionVertexAttr(string[] tokens)
        {
            switch (tokens[0])
            {
                case "uid":
                    {
                        // uid {
                        // ...
                        // }
                        if (tokens[1] != "{")
                            Error(tokens);

                        DoRead(SectionUid);
                    }
                    break;
                case "}":
                    return false;
            }
            return true;
        }

        bool SectionUid(string[] tokens)
        {
            switch (tokens[0])
            {
                case "}":
                    return false;
            }
            return true;
        }

        bool SectionFace(string[] tokens)
        {
            if (tokens[0] == "}")
                return false;

            int nface = int.Parse(tokens[0]);
            {
                StringBuilder sb = new StringBuilder();
                foreach (string i in tokens)
                    sb.Append(' ').Append(i);
                string line = sb.ToString().Trim();
                tokens = SplitString(line);
            }
            switch (nface)
            {
                case 3:
                    {
                        MqoFace f = new MqoFace();

                        for (int i = 1; i < tokens.Length; ++i)
                        {
                            string t = tokens[i];
                            string t2 = t.ToLower();

                            if (t2.StartsWith("v("))
                            {
                                string[] t3 = SplitParam(t);
                                f.a = ushort.Parse(t3[1]);
                                f.b = ushort.Parse(t3[2]);
                                f.c = ushort.Parse(t3[3]);
                            }
                            else if (t2.StartsWith("m("))
                            {
                                string[] t3 = SplitParam(t);
                                f.mtl = ushort.Parse(t3[1]);
                            }
                            else if (t2.StartsWith("uv("))
                            {
                                string[] t3 = SplitParam(t);
                                f.ta = Point2.Parse(t3, 1);
                                f.tb = Point2.Parse(t3, 3);
                                f.tc = Point2.Parse(t3, 5);
                            }
                        }
                        current.faces.Add(f);
                    }
                    break;
                case 4:
                    {
                        MqoFace f = new MqoFace();
                        MqoFace f2 = new MqoFace();

                        for (int i = 1; i < tokens.Length; ++i)
                        {
                            string t = tokens[i];
                            string t2 = t.ToLower();

                            if (t2.StartsWith("v("))
                            {
                                string[] t3 = SplitParam(t);
                                f.a = ushort.Parse(t3[1]);
                                f.b = ushort.Parse(t3[2]);
                                f.c = ushort.Parse(t3[3]);
                                f2.a = f.a;
                                f2.b = f.c;
                                f2.c = ushort.Parse(t3[4]);
                            }
                            else if (t2.StartsWith("m("))
                            {
                                string[] t3 = SplitParam(t);
                                f.mtl = ushort.Parse(t3[1]);
                                f2.mtl = f.mtl;
                            }
                            else if (t2.StartsWith("uv("))
                            {
                                string[] t3 = SplitParam(t);
                                f.ta = Point2.Parse(t3, 1);
                                f.tb = Point2.Parse(t3, 3);
                                f.tc = Point2.Parse(t3, 5);
                                f2.ta = f.ta;
                                f2.tb = f.tc;
                                f2.tc = Point2.Parse(t3, 7);
                            }
                        }
                        current.faces.Add(f);
                        current.faces.Add(f2);
                    }
                    break;
            }
            return true;
        }
    }

    public class MqoScene
    {
        public Point3 pos;
        public Point3 lookat;
        public float head;
        public float pich;
        public float ortho;
        public float zoom2;
        public Color3 amb;
    }

    public class MqoMaterial
    {
        public string name;
        public int shader;
        public Color3 col;
        public float dif;
        public float amb;
        public float emi;
        public float spc;
        public float power;
        public string tex;

        public MqoMaterial() { }
        public MqoMaterial(string n) { name = n; }
    }

    public class MqoObject
    {
        public string name;
        public int visible;
        public int locking;
        public int shading;
        public float facet;
        public Color3 color;
        public int color_type;
        public List<UVertex> vertices;
        public List<MqoFace> faces;

        public MqoObject() { }
        public MqoObject(string n) { name = n; }

        public void CreateNormal()
        {
            // 法線生成
            Point3[] normal = new Point3[vertices.Count];

            foreach (MqoFace face in faces)
            {
                Point3 v1 = Point3.Normalize(vertices[face.b].Pos - vertices[face.a].Pos);
                Point3 v2 = Point3.Normalize(vertices[face.c].Pos - vertices[face.b].Pos);
                Point3 n = Point3.Normalize(Point3.Cross(v1, v2));
                normal[face.a] -= n;
                normal[face.b] -= n;
                normal[face.c] -= n;
            }

            for (int i = 0; i < normal.Length; ++i)
                vertices[i].Nrm = Point3.Normalize(normal[i]);
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

    public class MqoFace
    {
        public ushort a, b, c, mtl;
        public Point2 ta, tb, tc;

        public MqoFace()
        {
        }

        public MqoFace(ushort a, ushort b, ushort c, ushort mtl, Point2 ta, Point2 tb, Point2 tc)
        {
            this.a = a;
            this.b = b;
            this.c = c;
            this.mtl = mtl;
            this.ta = ta;
            this.tb = tb;
            this.tc = tc;
        }

        public void Write(TextWriter tw)
        {
            tw.WriteLine("\t\t{0} V({1} {2} {3}) M({10}) UV({4:F5} {5:F5} {6:F5} {7:F5} {8:F5} {9:F5})",
                3, a, b, c, ta.x, ta.y, tb.x, tb.y, tc.x, tc.y, mtl);
        }
    }
}
