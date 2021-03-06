using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Tso2MqoGui
{
    public class MqoFile
    {
        public MqoScene scene;
        public List<MqoMaterial> materials;
        public List<MqoObject> objects = new List<MqoObject>();
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
        public int id; //object_id
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
        public int id; //vertex_id
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
        public ushort[] vert_indices;
        public ushort spec;
        public Point2[] texcoords;

        public ushort a { get { return vert_indices[0]; } set { vert_indices[0] = value; } }
        public ushort b { get { return vert_indices[1]; } set { vert_indices[1] = value; } }
        public ushort c { get { return vert_indices[2]; } set { vert_indices[2] = value; } }
        public Point2 ta { get { return texcoords[0]; } set { texcoords[0] = value; } }
        public Point2 tb { get { return texcoords[1]; } set { texcoords[1] = value; } }
        public Point2 tc { get { return texcoords[2]; } set { texcoords[2] = value; } }

        public MqoFace()
        {
            vert_indices = new ushort[3];
            texcoords = new Point2[3];
        }

        public MqoFace(ushort a, ushort b, ushort c, ushort spec, Point2 ta, Point2 tb, Point2 tc)
        {
            vert_indices = new ushort[3];
            vert_indices[0] = a;
            vert_indices[1] = b;
            vert_indices[2] = c;
            this.spec = spec;
            texcoords = new Point2[3];
            texcoords[0] = ta;
            texcoords[1] = tb;
            texcoords[2] = tc;
        }

        public void Write(TextWriter tw)
        {
            tw.WriteLine("\t\t{0} V({1} {2} {3}) M({10}) UV({4:F5} {5:F5} {6:F5} {7:F5} {8:F5} {9:F5})",
                3, a, b, c, ta.x, ta.y, tb.x, tb.y, tc.x, tc.y, spec);
        }
    }
}
