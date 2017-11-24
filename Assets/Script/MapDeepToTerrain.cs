using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
using System.Linq;

public class MapDeepToTerrain : MonoBehaviour {
    private int HEIGHT_KINECT = 240;
    private int WIDTH_KINECT = 320;
    static int MIN_DIMEN = 240;
    readonly int SKIP_FRAMES_MIN_MAX = 30;
    readonly int SKIP_FRAMES_MAPCOLOR = 10;
    readonly int SKIP_FRAMES_MAPHEIGHT = 5;
    readonly float NORMALIZE_RAW_DATA = 6000.0f;

    float[,] data; 
    short[] DepthImage;

    public DepthWrapper KinectDepth;
    public int maxHeightMap = 100;
    public float heightOffset = 0.03f;
    private bool checkToWriteFile = true;
    private int countFrameMinMax = 0;
    private int countFrameMapColor = 0;
    private int countFrameMapHeight = 0;
    private float maxVal = 0;
    private float minVal = 0;

    // Use this for initialization
    void Start() {
        MIN_DIMEN = Math.Min(HEIGHT_KINECT, WIDTH_KINECT);
        data = new float[MIN_DIMEN, MIN_DIMEN];
    }

    // Update is called once per frame
    void Update()
    {
        if (KinectDepth.pollDepth())
        {
            DepthImage = KinectDepth.depthImg;
           
            //loadDeep(GetComponent<Terrain>().terrainData, result, false);
        
            loadDeep(GetComponent<Terrain>().terrainData, DepthImage);

            //WriteTerrainHeightMap();
        }
    }

    void loadDeep(TerrainData tData, byte[] rawData, bool adjustResolution = false)
    {
        int h = (int)Mathf.Sqrt((float)rawData.Length / 2);
        if (adjustResolution)
        {
            var size = tData.size;
            tData.heightmapResolution = h;
            tData.size = size;
        }
        else if (h > tData.heightmapHeight)
        {
            h = tData.heightmapHeight;
        }
        int w = h;

        float[,] data = new float[h, w];
        int i = 0;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int u;

                // little-endian (windows)
                u = rawData[i + 1] << 8 | rawData[i];

                float v = (float)u / 0xFFFF;
                data[y, x] = v;
                i += 2;
            }
        }

        tData.SetHeights(0, 0, data);
       
    }

    void loadDeep(TerrainData tData, short[] rawData)
    {
        int i = rawData.Length - 1;
        if (countFrameMinMax == 0)
        {
            maxVal = (float)rawData.Max() / NORMALIZE_RAW_DATA;
            minVal = (float)rawData.Min() / NORMALIZE_RAW_DATA;
        }
        countFrameMinMax =  (countFrameMinMax + 1) % SKIP_FRAMES_MIN_MAX;

        if (countFrameMapHeight == 0)
        {
            for (int y = 0; y < MIN_DIMEN; y++)
            {
                for (int x = 0; x < MIN_DIMEN; x++)
                {
                    //flatten background
                    data[y, x] = (rawData[i] >= minVal && rawData[i] <= minVal + heightOffset) ? minVal : maxVal - (float)rawData[i] / NORMALIZE_RAW_DATA;
                    //data[y, x] = (y + x) / 6000.0f;
                    i--;
                }
                i = i - 80;
            }
            tData.size = new Vector3(MIN_DIMEN, maxHeightMap, MIN_DIMEN);
            tData.SetHeights(0, 0, data);
        }
        countFrameMapHeight = (countFrameMapHeight + 1) % SKIP_FRAMES_MAPHEIGHT;

        if (countFrameMapColor == 0)
        {
            mapColor(tData);
        }
        countFrameMapColor = (countFrameMapColor + 1) % SKIP_FRAMES_MAPCOLOR;
    }
	
	private void mapColor(TerrainData terrainData)
    {
        // Splatmap data is stored internally as a 3d array of floats, so declare a new empty array ready for your custom splatmap data:
        float[,,] splatmapData = new float[terrainData.alphamapWidth, terrainData.alphamapHeight, terrainData.alphamapLayers];

        for (int y = 0; y < terrainData.alphamapHeight; y++)
        {
            for (int x = 0; x < terrainData.alphamapWidth; x++)
            {
                // Normalise x/y coordinates to range 0-1 
                float y_01 = (float)y / (float)terrainData.alphamapHeight;
                float x_01 = (float)x / (float)terrainData.alphamapWidth;

                // Sample the height at this location (note GetHeight expects int coordinates corresponding to locations in the heightmap array)
                //float height = terrainData.GetHeight(Mathf.RoundToInt(y_01 * terrainData.heightmapHeight), Mathf.RoundToInt(x_01 * terrainData.heightmapWidth));
                float height = terrainData.GetHeight(y, x);
                // Calculate the normal of the terrain (note this is in normalised coordinates relative to the overall terrain dimensions)
                Vector3 normal = terrainData.GetInterpolatedNormal(y_01, x_01);

                // Calculate the steepness of the terrain
                float steepness = terrainData.GetSteepness(y_01, x_01);

                // Setup an array to record the mix of texture weights at this point
                float[] splatWeights = new float[terrainData.alphamapLayers];

                // CHANGE THE RULES BELOW TO SET THE WEIGHTS OF EACH TEXTURE ON WHATEVER RULES YOU WANT

                // Texture[0] has constant influence
                splatWeights[0] = 0.1f;

                // Texture[1] is stronger at lower altitudes
                splatWeights[1] = Mathf.Clamp01((terrainData.heightmapHeight - height));

                // Texture[2] stronger on flatter terrain
                // Note "steepness" is unbounded, so we "normalise" it by dividing by the extent of heightmap height and scale factor
                // Subtract result from 1.0 to give greater weighting to flat surfaces
                splatWeights[2] = 1.0f - Mathf.Clamp01(steepness * steepness / (terrainData.heightmapHeight / 5.0f));

                // Texture[3] increases with height but only on surfaces facing positive Z axis 
                //splatWeights[3] = height * Mathf.Clamp01(normal.z);

                // Sum of all textures weights must add to 1, so calculate normalization factor from sum of weights
                float z = splatWeights.Sum();

                // Loop through each terrain texture
                for (int i = 0; i < terrainData.alphamapLayers; i++)
                {

                    // Normalize so that sum of all texture weights = 1
                    splatWeights[i] /= z;

                    // Assign this point to the splatmap array
                    splatmapData[x, y, i] = splatWeights[i];
                }
            }
        }

        // Finally assign the new splatmap to the terrainData:
        terrainData.SetAlphamaps(0, 0, splatmapData);
    }

    void WriteTerrainHeightMap(TerrainData terrainData)
    {
        if (checkToWriteFile)
        {
            checkToWriteFile = false;
            using (FileStream fs = new FileStream("a.txt", FileMode.CreateNew, FileAccess.Write))
            using (StreamWriter sw = new StreamWriter(fs))
            {

                for (int i = 0; i < terrainData.heightmapHeight; i++)
                {
                    for (int j = 0; j < terrainData.heightmapWidth; j++)
                    {
                        sw.Write(terrainData.GetHeight(i, j));
                        sw.Write(" ");
                    }
                    sw.WriteLine();
                }


                sw.Close();
            }

        }
    }

    private void guestThreshodMapColor ()
    {
       
    }

}

