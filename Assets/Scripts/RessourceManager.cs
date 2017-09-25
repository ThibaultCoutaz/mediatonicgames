using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;
using UnityEngine.UI;
using System.Threading;

public class RessourceManager : MonoBehaviour {
    public bool startThreadWhenDownload;
    public Text infos;

    [System.Serializable]
    public struct Objects
    {
        public MeshRenderer tex;
        public string URL;
        [HideInInspector]
        public bool needSave;
        [HideInInspector]
        public string pathSave;
        [HideInInspector]
        public byte[] saveData;
        [HideInInspector]
        public bool DataReady;

    }

    public Objects[] listObjects;
    public int nbThread;

    private WWW www;
    private Thread[] listThread;
    private Texture2D tmpTex;
    private List<int> indexObjectsWaiting;
    private List<int> indexObjectsWaitingSave;
    private List<int> indexObjectsWaitingLoad;
    private List<int> indexFreeThread;
    private int texDraw = 0;
    private bool allDraw = false;

    private List<int> iFor;

    private int tmpI, tmpIndex;
    // Use this for initialization
    IEnumerator Start()
    {

        iFor = new List<int>();

        tmpTex = new Texture2D(8, 8);

        indexFreeThread = new List<int>();
        listThread = new Thread[nbThread];
        for (int i = 0; i < listThread.Length; i++)
        {
            indexFreeThread.Add(i);
        }

        if (startThreadWhenDownload)
        {
            indexObjectsWaiting = new List<int>();

            for (int i = 0; i < listObjects.Length; i++)
            {
                listObjects[i].DataReady = false;
                listObjects[i].pathSave = Application.persistentDataPath + listObjects[i].tex.gameObject.name + ".jpg";
                if (File.Exists(listObjects[i].pathSave))
                {
                    infos.text = "Loading";
                    listObjects[i].needSave = false;

                    if (indexFreeThread.Count > 0)
                    {
                        iFor.Add(i); //To be sure that multiple thread are not using the same value of i
                        tmpIndex = indexFreeThread[0];
                        indexFreeThread.RemoveAt(0);

                        listThread[tmpIndex] = new Thread(() => LoadTexture(tmpIndex));
                        listThread[tmpIndex].Start();
                    }
                    else
                    {
                        indexObjectsWaiting.Add(i);
                    }
                }
                else
                {
                    infos.text = "Downloading";
                    www = new WWW(listObjects[i].URL);
                    yield return www;

                    if (string.IsNullOrEmpty(www.error))
                    {
                        listObjects[i].saveData = www.texture.EncodeToJPG();

                        listObjects[i].needSave = true;

                        if (indexFreeThread.Count > 0)
                        {
                            iFor.Add(i);
                            tmpIndex = indexFreeThread[0];
                            indexFreeThread.RemoveAt(0);
                            listThread[tmpIndex] = new Thread(() => SaveTexture(tmpIndex));
                            listThread[tmpIndex].Start();
                        }
                        else
                        {
                            indexObjectsWaiting.Add(i);
                        }
                    }
                    else
                    {
                        listObjects[i].saveData = null;
                    }
                }
            }

            infos.text = "DONE !";
        }
        else
        {
            indexObjectsWaitingSave = new List<int>();
            indexObjectsWaitingLoad = new List<int>();

            for (int i = 0; i < listObjects.Length; i++)
            {
                listObjects[i].pathSave = Application.persistentDataPath + listObjects[i].tex.gameObject.name + ".jpg";
                if (!File.Exists(listObjects[i].pathSave))
                {
                    infos.text = "Downloading";
                    www = new WWW(listObjects[i].URL);
                    yield return www;

                    if (string.IsNullOrEmpty(www.error))
                    {

                        listObjects[i].saveData = www.texture.EncodeToJPG();

                        if (indexFreeThread.Count > 0)
                        {
                            iFor.Add(i);
                            tmpIndex = indexFreeThread[0];
                            indexFreeThread.RemoveAt(0);
                            listThread[tmpIndex] = new Thread(() => SaveTextureWithoutLoad(tmpIndex));
                            listThread[tmpIndex].Start();
                        }
                        else
                        {
                            indexObjectsWaitingSave.Add(i);
                        }
                    }
                    else
                    {
                        listObjects[i].saveData = null;
                    }
                }
            }

            infos.text = "Loading !";

            //To be sure We wait to have at least one thread free to start the loading other way it can happen that none of the thread will get free during all the loading and so no loading will appear
            while (indexFreeThread.Count == 0) { }

            iFor.Clear();

            for (int i = 0; i < listObjects.Length; i++)
            {
                if (listObjects[i].saveData != null)//To check if it was download
                {
                    listObjects[i].DataReady = false;
                    listObjects[i].pathSave = Application.persistentDataPath + listObjects[i].tex.gameObject.name + ".jpg";
                    listObjects[i].needSave = false;

                    if (indexFreeThread.Count > 0)
                    {
                        iFor.Add(i);
                        tmpIndex = indexFreeThread[0];
                        indexFreeThread.RemoveAt(0);

                        listThread[tmpIndex] = new Thread(() => LoadTextureWithoutSave(tmpIndex));
                        listThread[tmpIndex].Start();
                    }
                    else
                    {
                        indexObjectsWaitingLoad.Add(i);
                    }
                }
            }

            infos.text = "DONE !";
        }
    }

    // Update is called once per frame
    void Update()
    {

        if (!allDraw)
            for (int i = 0; i < listObjects.Length; i++)
            {
                if (listObjects[i].DataReady)
                {
                    tmpTex.LoadImage(listObjects[i].saveData);
                    listObjects[i].tex.material.mainTexture = tmpTex;
                    listObjects[i].DataReady = false;
                    tmpTex = new Texture2D(8, 8);
                    texDraw++;
                    if (texDraw >= listObjects.Length)
                        allDraw = true;
                }
            }
    }

    private void SaveTexture(int indexThread)
    {
        int tmp;
        lock (_lockTmp)
            tmp = iFor[0];

        listObjects[tmp].needSave = false;

        //byte[] bytes = listTex[_i].EncodeToJPG();
        File.WriteAllBytes(listObjects[tmp].pathSave, listObjects[tmp].saveData);
        LoadTexture(indexThread);
    }

    private void LoadTexture(int indexThread)
    {
        int tmp;
        lock (_lockTmp)
        {
            tmp = iFor[0];
            iFor.RemoveAt(0);
        }
        Debug.LogError(tmp);

        listObjects[tmp].saveData = File.ReadAllBytes(listObjects[tmp].pathSave);
        listObjects[tmp].DataReady = true;

        if (indexObjectsWaiting.Count > 0)
        {
            int tmpI;
            lock (_lockAdd)
            {
                tmpI = indexObjectsWaiting[0];
                iFor.Add(indexObjectsWaiting[0]);
                indexObjectsWaiting.RemoveAt(0);
            }

            if (listObjects[tmpI].needSave)
                SaveTexture(indexThread);
            else
                LoadTexture(indexThread);
        }
        else
        {
            indexFreeThread.Add(indexThread);
        }
    }

    object _lockTmp = new object();
    object _lockAdd = new object();

    private void SaveTextureWithoutLoad(int indexThread)
    {
        int tmp;
        lock (_lockTmp)
        {
            tmp = iFor[0];
            iFor.RemoveAt(0);
        }

        File.WriteAllBytes(listObjects[tmp].pathSave, listObjects[tmp].saveData);

        if (indexObjectsWaitingSave.Count > 0)
        {
            lock (_lockAdd)
            {
                iFor.Add(indexObjectsWaitingSave[0]);
                indexObjectsWaitingSave.RemoveAt(0);
            }

            SaveTextureWithoutLoad(indexThread);
        }
        else
        {
            indexFreeThread.Add(indexThread);
        }
    }

    private void LoadTextureWithoutSave(int indexThread)
    {
        int tmp;
        lock (_lockTmp)
        {
            Debug.Log(iFor[0]);
            tmp = iFor[0];
            iFor.RemoveAt(0);
        }

        listObjects[tmp].saveData = File.ReadAllBytes(listObjects[tmp].pathSave);
        listObjects[tmp].DataReady = true;

        if (indexObjectsWaitingLoad.Count > 0)
        {
            lock (_lockAdd)
            {
                iFor.Add(indexObjectsWaitingLoad[0]);
                indexObjectsWaitingLoad.RemoveAt(0);
            }

            LoadTextureWithoutSave(indexThread);
        }
        else
        {
            indexFreeThread.Add(indexThread);
        }
    }
}
