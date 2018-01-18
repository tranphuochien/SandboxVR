using UnityEngine;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Collections;
using Firebase.Storage;
using System.Drawing;

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
    private const int epsilon = 2;

    public DepthWrapper KinectDepth;
    public int maxHeightMap = 100;
    public float heightOffset = 0.03f;
    private bool checkToWriteFile = true;
    private int countFrameMinMax = 0;
    private int countFrameMapColor = 0;
    private int countFrameMapHeight = 0;
    private float maxVal = 0;
    private float minVal = 0;
    private static int currentMax = 0;
    private GameObject mRain;
    private GameObject mWater;
    private float mMinYPosWater = -5f;
    private float mMaxYPosWater = 20f;
    private static bool isRaining;
    private const int DRAINAGE_INTERVAL = 20;
    private static int mDrainageCount = 0;
    private static int oldMax = 0;
    private static int oldMin = 0;
    private static bool isChanged = false;
    private static List<TreeInstance> treeList = new List<TreeInstance>(0);
    private static int placedTrees = 0;

    // Use this for initialization
    void Start() {
        MIN_DIMEN = Math.Min(HEIGHT_KINECT, WIDTH_KINECT);
        data = new float[MIN_DIMEN, MIN_DIMEN];
        mRain = this.gameObject.transform.GetChild(0).gameObject;
        mWater = this.gameObject.transform.GetChild(1).gameObject;

        //renderTree(GetComponent<Terrain>().terrainData);  
    }

    // Update is called once per frame
    void Update()
    {
        //mWater.transform.Translate(Vector3.up);

        /*if (mWater.transform.position.y > -5)
        {
            mWater.transform.Translate(-Vector3.up);
        }*/

        mDrainageCount = ++mDrainageCount % DRAINAGE_INTERVAL;
        float currentWaterY = mWater.transform.position.y;

        if (isRaining && currentWaterY < mMaxYPosWater)
        {
            if (mDrainageCount >= DRAINAGE_INTERVAL - 1)
            {
                mWater.transform.Translate(Vector3.up);
            }
        }
        if (!isRaining && currentWaterY > mMinYPosWater)
        {
            if (mDrainageCount >= DRAINAGE_INTERVAL - 1)
            {
                mWater.transform.Translate(-Vector3.up);
            }
        }



        if (KinectDepth.pollDepth())
        {
            DepthImage = KinectDepth.depthImg;
           
            //loadDeep(GetComponent<Terrain>().terrainData, result, false);
        
            loadDeep(GetComponent<Terrain>().terrainData, DepthImage);
            Debug.Log("height: " + GetComponent<Terrain>().terrainData.alphamapHeight);
            Debug.Log("width: " + GetComponent<Terrain>().terrainData.alphamapWidth);
            //WriteTerrainHeightMap();
            //renderTree(GetComponent<Terrain>().terrainData);
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
                // to make terrain square
                i = i - 80;
            }
            tData.size = new Vector3(MIN_DIMEN, maxHeightMap, MIN_DIMEN);
            tData.SetHeights(0, 0, data);
        }
        countFrameMapHeight = (countFrameMapHeight + 1) % SKIP_FRAMES_MAPHEIGHT;

        //WriteTerrainHeightMap(tData);
        //if (checkToWriteFile)
        //{
        //    checkToWriteFile = false;
        //    guestThresholdMapColor(tData);
        //}

        if (countFrameMapColor == 0)
        {
            KeyValuePair<int, int> threshold = guestThresholdMapColor(tData);
            mapColor(tData, threshold);
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

    private void mapColor(TerrainData terrainData, KeyValuePair<int, int> threshold)
    {
        int diffThreshold;
        

        if (currentMax != 0)
        {
            if (threshold.Value - currentMax <= epsilon)
            {
                currentMax = threshold.Value;

                mRain.SetActive(false);
                isRaining = false;
            } else
            {
                mRain.SetActive(true);
                isRaining = true;
            }
            diffThreshold = currentMax - threshold.Key;
        } else
        {
            diffThreshold = threshold.Value - threshold.Key;
            currentMax = threshold.Value;
        }
        Debug.Log("min: " + threshold.Key + "max: " + threshold.Value);

        if (oldMax != threshold.Value || oldMin != threshold.Key)
        {
            isChanged = true;
            oldMax = threshold.Value;
            oldMin = threshold.Key;
            if (treeList != null && treeList.Count != 0)
            {
                deleteAllTree();
            }
        } else
        {
            isChanged = false;
        }
        
        float[,,] splatmapData = new float[terrainData.alphamapWidth, terrainData.alphamapHeight, terrainData.alphamapLayers];

        for (int y = 0; y < terrainData.alphamapHeight; y++)
        {
            for (int x = 0; x < terrainData.alphamapWidth; x++)
            {
                // Normalise x/y coordinates to range 0-1 
                float y_01 = (float)y / (float)terrainData.alphamapHeight;
                float x_01 = (float)x / (float)terrainData.alphamapWidth;

                float height = terrainData.GetHeight(y, x);
             
                // Setup an array to record the mix of texture weights at this point
                float[] splatWeights = new float[terrainData.alphamapLayers];

                handleSetWeights(height, diffThreshold, 5, splatWeights, placedTrees, treeList, x, y, terrainData.heightmapHeight, terrainData.heightmapWidth);

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
        terrainData.treeInstances = treeList.ToArray();
        terrainData.SetHeights(0, 0, new float[,] { { } });
    }

    private float[] handleSetWeights(double height, double diffThreshold, int nLayers, float[] splatWeights, int placedTrees, List<TreeInstance> treeList, int x, int y, int mapHeight, int mapWidth)
    {
        double a = height * 1.0f / (diffThreshold / 5.0f);
     
        for ( int i = nLayers - 1; i >= 0; i--)
        {
            if (a >= i * 1.0f)
            {
                if (i != 0)
                {
                    double tmp = a - i * 1.0f;
                    splatWeights[i] = (float)tmp;
                    splatWeights[i - 1] = (float)(1.0 - tmp);
                    if (i == 2 && splatWeights[i] > 0.89 && isChanged)
                    {
                        addTree(placedTrees, treeList, x, y, mapHeight, mapWidth);
                    }
                }
                else
                {
                    splatWeights[i] = 1.0f;
                }
                break;
            }
        }
        return splatWeights;
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

    private KeyValuePair<int, int> guestThresholdMapColor (TerrainData tData)
    {
        int [] mappingVal = new int[100];
        int min = 0, max = 0;

        for (int i = 0; i < tData.heightmapHeight; i++)
        {
            for (int j = 0; j < tData.heightmapWidth; j++)
            {
                float tmp = tData.GetHeight(i, j);
                mappingVal[(int)tmp]++;
            }
        }

        for(int i = 1; i < 100; i++)
        {
            if (mappingVal[i] != 0)
            {
                min = i;
                break;
            }
        }
        for (int i = 99; i> 0; i--)
        {
            if (mappingVal[i] != 0)
            {
                max = i;
                break;
            }
        }
        //using (FileStream fs = new FileStream("a.txt", FileMode.CreateNew, FileAccess.Write))
        //using (StreamWriter sw = new StreamWriter(fs))
        //{

        //    for (int i = 0; i < mappingVal.Length; i++)
        //    {
        //            sw.WriteLine(i + "   " + mappingVal[i]);   
        //    }
        //    sw.Close();
        //}
        return new KeyValuePair<int, int>(min, max) ;
    }

    private void deleteAllTree()
    {
        List<TreeInstance> newTrees = new List<TreeInstance>(0);
        placedTrees = 0;
        GetComponent<Terrain>().terrainData.treeInstances = newTrees.ToArray();
    }

    private void renderTree(TerrainData terrainData)
    {
        List<TreeInstance> treeList = new List<TreeInstance>(0);

        int placedTrees = 0;

        for (int i = 0; i < terrainData.heightmapHeight/10; i++)
        {
            for (int j = 0; j < terrainData.heightmapWidth/10; j++)
            {
                float percent = UnityEngine.Random.value;
                if (percent > 0.99f)
                {
                    placedTrees++;
                  
                    //Vector3 treePos = new Vector3(0.0f + placedTrees / terrainData.heightmapWidth, 0.0f, 0.0f + placedTrees / terrainData.heightmapHeight);
                    Vector3 treePos = new Vector3(percent , 0.0f, percent);
                    TreeInstance tree = new TreeInstance();
                 
                    tree.position = treePos;
                    tree.prototypeIndex = 0;
                    tree.color = new UnityEngine.Color(1, 1, 1);
                    tree.lightmapColor = new UnityEngine.Color(1, 1, 1);
                    tree.heightScale = 1;
                    tree.widthScale = 1;

                    treeList.Add(tree);
                }
            }
        }
        //run after the loop
        Debug.Log("trees placed: " + placedTrees);
        Debug.Log("tree array size: " + treeList.Count);
        terrainData.treeInstances = treeList.ToArray();
        terrainData.SetHeights(0, 0, new float[,] { { } });
    }

    private List<TreeInstance> addTree(int placedTrees, List<TreeInstance> treeList, int x, int y, int mapHeight, int mapWidth)
    {
        float percent = UnityEngine.Random.value;
        if (percent > 0.99f)
        {
            placedTrees++;
            float x1 = x * 1.0f / mapWidth;
            float y1 = y * 1.0f / mapHeight;
            float x3 = y1;
            float y3 = x1;

            //Vector3 treePos = new Vector3(0.0f + placedTrees / terrainData.heightmapWidth, 0.0f, 0.0f + placedTrees / terrainData.heightmapHeight);
            Vector3 treePos = new Vector3(x3, 0.0f, y3);
            //Vector3 treePos = new Vector3(x2, 0.0f, y2);
            TreeInstance tree = new TreeInstance();

            tree.position = treePos;
            tree.prototypeIndex = 0;
            tree.color = new UnityEngine.Color(1, 1, 1);
            tree.lightmapColor = new UnityEngine.Color(1, 1, 1);
            tree.heightScale = 1;
            tree.widthScale = 1;

            treeList.Add(tree);
        }

        return treeList;
    }
}

