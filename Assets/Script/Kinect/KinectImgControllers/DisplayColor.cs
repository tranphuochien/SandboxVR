using UnityEngine;
using System.Collections;
using System.Drawing;



[RequireComponent(typeof(Renderer))]
public class DisplayColor : MonoBehaviour {
	
	public DeviceOrEmulator devOrEmu;
	private Kinect.KinectInterface kinect;
	
	private Texture2D tex;
    private bool shouldWrite = true;
	
	// Use this for initialization
	void Start () {
		kinect = devOrEmu.getKinect();
		//tex = new Texture2D(640,480,TextureFormat.ARGB32,false);
		tex = new Texture2D(320,240,TextureFormat.ARGB32,false);
		GetComponent<Renderer>().material.mainTexture = tex;
		
	}
	
	// Update is called once per frame
	void Update () {
		if (kinect.pollColor())
		{
			//tex.SetPixels32(kinect.getColor());
            if (shouldWrite)
            {
                shouldWrite = false;
                writePicture(mipmapImg(kinect.getColor(), 640, 480));
            }
			tex.SetPixels32(mipmapImg(kinect.getColor(),640,480));
        
			tex.Apply(false);
		}
	}

    void writePicture(Color32[] src)
    {
        //wid: 320
        Bitmap bmp = new Bitmap(240, 240);
        int idx = 0;
        for (int y = 0; y < 240; y++)
        {
            for (int x = 0; x < 240; x++)
            {

                System.Drawing.Color color = System.Drawing.Color.FromArgb(src[idx].r, src[idx].g, src[idx].b);
                bmp.SetPixel(x, y, color);

                idx++;
            }
            idx += 80;
        }

        using (Bitmap b = bmp)
        {
            //using (System.Drawing.Graphics g = System.Drawing.Graphics.FromImage(b))
            //{
            //    g.Clear(System.Drawing.Color.Green);
            //}
            b.Save(@"E:\green.png", System.Drawing.Imaging.ImageFormat.Png);
        }
    }

    private Color32[] mipmapImg(Color32[] src, int width, int height)
	{
		int newWidth = width / 2;
		int newHeight = height / 2;
		Color32[] dst = new Color32[newWidth * newHeight];
		for(int yy = 0; yy < newHeight; yy++)
		{
			for(int xx = 0; xx < newWidth; xx++)
			{
				int TLidx = (xx * 2) + yy * 2 * width;
				int TRidx = (xx * 2 + 1) + yy * width * 2;
				int BLidx = (xx * 2) + (yy * 2 + 1) * width;
				int BRidx = (xx * 2 + 1) + (yy * 2 + 1) * width;
				dst[xx + yy * newWidth] = Color32.Lerp(Color32.Lerp(src[BLidx],src[BRidx],.5F),
				                                       Color32.Lerp(src[TLidx],src[TRidx],.5F),.5F);
			}
		}
		return dst;
	}
	
	public Texture2D GetCurrentTexture()
	{
		Texture2D t = new Texture2D(320,240,TextureFormat.ARGB32,false);
		t.SetPixels32(mipmapImg(kinect.getColor(),640,480));
		t.Apply(false);
		return t;
	}
}
