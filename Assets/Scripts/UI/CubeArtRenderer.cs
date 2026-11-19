using System.Collections.Generic;
using UnityEngine;

namespace DrawArrow
{
    // The original face/voxel data is rendered as opaque geometry, not a projected wireframe.
    public sealed class CubeArtRenderer : MonoBehaviour
    {
        const int Layer=30;GameObject root;Camera view;RenderTexture target;ArrowBoard board;
        readonly List<LineRenderer> lines=new List<LineRenderer>();readonly List<LineRenderer> heads=new List<LineRenderer>();
        Material surface,brown,blue;Vector3 center;Rect destination;
        public void Draw(Rect rect,ArrowBoard source,float yaw,float pitch,float roll,float zoom,int hint,float until)
        {
            if(board!=source)Build(source);destination=rect;
            var rotation=Quaternion.Euler(-pitch,yaw,roll);float diagonal=Mathf.Sqrt(source.Level.width*source.Level.width+source.Level.height*source.Level.height+source.Level.depth*source.Level.depth);
            view.transform.position=root.transform.position+Quaternion.Inverse(rotation)*new Vector3(0,0,-diagonal*2.3f);view.transform.LookAt(root.transform.position,Quaternion.Inverse(rotation)*Vector3.up);
            view.aspect=rect.width/rect.height;
            var half=center+Vector3.one*.025f;float extentX=0,extentY=0;
            for(int corner=0;corner<8;corner++)
            {
                var local=new Vector3((corner&1)==0?-half.x:half.x,(corner&2)==0?-half.y:half.y,(corner&4)==0?-half.z:half.z);
                var cameraPoint=view.transform.InverseTransformPoint(root.transform.position+local);
                extentX=Mathf.Max(extentX,Mathf.Abs(cameraPoint.x));extentY=Mathf.Max(extentY,Mathf.Abs(cameraPoint.y));
            }
            view.orthographicSize=Mathf.Max(.1f,Mathf.Max(extentY,extentX/view.aspect))*1.05f/zoom;view.backgroundColor=CircuitStyle.Background;
            for(int i=0;i<lines.Count;i++)
            {
                bool active=source.Active[i]&&!source.IsMechanism(i);lines[i].enabled=active;heads[i].enabled=active;
                var material=i==hint&&Time.unscaledTime<until?blue:brown;lines[i].sharedMaterial=material;heads[i].sharedMaterial=material;
            }
            if(Event.current.type==EventType.Repaint)view.Render();GUI.DrawTexture(rect,target,ScaleMode.StretchToFill,false);
        }
        public Vector2 Project(Vector3 p)
        {
            var v=view.WorldToViewportPoint(root.transform.position+p-center);return new Vector2(destination.x+v.x*destination.width,destination.y+(1-v.y)*destination.height);
        }
        public bool Visible(Vector3 p)
        {
            var round=new Vector3(Mathf.Round(p.x),Mathf.Round(p.y),Mathf.Round(p.z));var normal=(p-round).normalized;
            return Vector3.Dot(normal,view.transform.position-(root.transform.position+p-center))>=-.02f;
        }
        void Build(ArrowBoard source)
        {
            board=source;if(root)Destroy(root);lines.Clear();heads.Clear();
            if (!surface)
            {
                var shader = Resources.Load<Shader>("Circuit/BoardMaterial");
                surface = new Material(shader) { color = Ink.Hex("E7E0FF") };
                surface.SetFloat("_Shade", 1);
                brown = new Material(shader) { color = Ink.Hex("392D79") };
                blue = new Material(shader) { color = Ink.Hex("00A991") };
            }
            root=new GameObject("Circuit 3D surface");root.layer=Layer;root.transform.position=new Vector3(5000,0,0);
            center=new Vector3((source.Level.width-1)*.5f,(source.Level.height-1)*.5f,(source.Level.depth-1)*.5f);
            var shell=new GameObject("Beveled shell");shell.layer=Layer;shell.transform.SetParent(root.transform,false);shell.AddComponent<MeshFilter>().sharedMesh=RoundedBox(center+Vector3.one*.025f,.15f);shell.AddComponent<MeshRenderer>().sharedMaterial=surface;
            for(int i=0;i<source.Paths.Length;i++)
            {
                var line=CreateLine("Arrow "+i,.115f);var points=source.Paths[i];line.positionCount=points.Count;for(int k=0;k<points.Count;k++)line.SetPosition(k,points[k]-center);lines.Add(line);
                var head=CreateLine("Head "+i,.125f);head.positionCount=3;
                if(points.Count>=2)
                {
                    var h=points[points.Count-1];var dir=source.Direction(i);var f=source.Level.arrows[i].points[0]%6;var side=Vector3.Cross(ArrowBoard.FaceNormal(f),dir).normalized;
                    head.SetPosition(0,h-dir*.44f+side*.27f-center);head.SetPosition(1,h-center);head.SetPosition(2,h-dir*.44f-side*.27f-center);
                }
                heads.Add(head);
                if(source.IsMechanism(i)&&points.Count>0)
                {
                    line.enabled=head.enabled=false;
                    var mark=CreateLine("Mechanism "+i,.12f);var p=points[0]-center;mark.positionCount=2;mark.SetPosition(0,p-Vector3.up*.14f);mark.SetPosition(1,p+Vector3.up*.14f);
                }
            }
            if(!view){view=new GameObject("Artwork preview camera").AddComponent<Camera>();view.enabled=false;view.orthographic=true;view.cullingMask=1<<Layer;view.clearFlags=CameraClearFlags.SolidColor;view.nearClipPlane=.1f;view.farClipPlane=500;target=new RenderTexture(1024,1024,24,RenderTextureFormat.ARGB32){antiAliasing=4};target.Create();view.targetTexture=target;}
        }
        LineRenderer CreateLine(string name,float width)
        {
            var obj=new GameObject(name);obj.layer=Layer;obj.transform.SetParent(root.transform,false);var l=obj.AddComponent<LineRenderer>();l.useWorldSpace=false;l.sharedMaterial=brown;l.widthMultiplier=width;l.numCapVertices=6;l.numCornerVertices=6;l.alignment=LineAlignment.View;return l;
        }
        static Mesh RoundedBox(Vector3 half,float radius)
        {
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            for(int f=0;f<6;f++)
            {
                Vector3 n=ArrowBoard.FaceNormal(f),u=f<2?Vector3.forward:Vector3.right,v=f<4?Vector3.up:Vector3.up;
                if(f==2||f==3)v=Vector3.forward;if(Vector3.Dot(Vector3.Cross(u,v),n)<0)u=-u;
                float hu=Vector3.Dot(new Vector3(Mathf.Abs(u.x),Mathf.Abs(u.y),Mathf.Abs(u.z)),half),hv=Vector3.Dot(new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z)),half),hn=Vector3.Dot(new Vector3(Mathf.Abs(n.x),Mathf.Abs(n.y),Mathf.Abs(n.z)),half);
                float[] xs={-hu,-hu+radius,hu-radius,hu},ys={-hv,-hv+radius,hv-radius,hv};int start=vertices.Count;
                for(int y=0;y<4;y++)for(int x=0;x<4;x++)
                {
                    var p=n*hn+u*xs[x]+v*ys[y];var inner=new Vector3(Mathf.Clamp(p.x,-half.x+radius,half.x-radius),Mathf.Clamp(p.y,-half.y+radius,half.y-radius),Mathf.Clamp(p.z,-half.z+radius,half.z-radius));var normal=(p-inner).normalized;vertices.Add(inner+normal*radius);normals.Add(normal);
                }
                for(int y=0;y<3;y++)for(int x=0;x<3;x++){int a=start+y*4+x;triangles.Add(a);triangles.Add(a+1);triangles.Add(a+5);triangles.Add(a);triangles.Add(a+5);triangles.Add(a+4);}
            }
            var mesh=new Mesh{name="Beveled shell"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();return mesh;
        }
        void OnDestroy(){if(root)Destroy(root);if(view)Destroy(view.gameObject);if(target){target.Release();Destroy(target);}if(surface)Destroy(surface);if(brown)Destroy(brown);if(blue)Destroy(blue);}
    }
}
