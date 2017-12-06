using UnityEngine;
using UnityEditor;
using System;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using UnityEditor.Animations;

public static class Exensions
{

}

public class GIFToAnimation_MenuItem
{

    [MenuItem("Assets/Create/GIFToAnimation", false, 11)]
    public static void GIFToAnimation()
    {

        GIFToAnimation_Window window = (GIFToAnimation_Window)EditorWindow.GetWindow(typeof(GIFToAnimation_Window), false, "GIFTA");
        window.ShowPopup();
        EditorWindow.FocusWindowIfItsOpen<GIFToAnimation_Window>();

        return;
    }
}

public static class Reimporter
{
    public static Action OnReimported;
    static Reimporter()
    {
        if (OnReimported != null)
            OnReimported();
    }
}

public class GIFToAnimation_Window : EditorWindow
{
    public static class Recompile
    {
        public static bool hasRecompiled;
        static Recompile()
        {
            hasRecompiled = true;
        }
    }
    public int padding;
    public int progress = 0;
    public int maxProgress = 1;
    public bool linkToPrefab;

    /// <summary>
    /// removes inset amount from string end ie. "example01" with inset at 2 would result in "example"
    /// </summary>
    /// <param name="me"></param>
    /// <param name="inset"></param>
    /// <returns></returns>
    public string InsetFromEnd(string me, int inset)
    {
        return me.Substring(0, me.Length - inset);
    }

    /// <summary>
    /// removes inset amounts from string ie. "0example01" with leftIn at 1 and with rightIn at 2 would result in "example"
    /// </summary>
    /// <param name="me"></param>
    /// <param name="inset"></param>
    /// <returns></returns>
    public string Inset(string me, int leftIn, int rightIn)
    {
        return me.Substring(leftIn, me.Length - rightIn - leftIn);
    }

    void OnGUI()
    {
        if (Selection.objects.Length == 0)
        {
            EditorGUILayout.LabelField("", "Select GIFs in Project Folder To Begin ");
            return;
        }
        padding = EditorGUILayout.IntField("Padding", padding);
        linkToPrefab = EditorGUILayout.Toggle("Link to Prefab", linkToPrefab);
        if (linkToPrefab)
        {
            GUILayout.Box("Name your .gif file\n\"PrefabName_AnimationName\"\nThis will add your animation to\nthe animator on that prefab.\nIf none exists it will add one.\nThis will save your sprite and \nanimation file(s) to the\nsame folder as your prefab", GUILayout.MinHeight(120f), GUILayout.MinWidth(300f));
        }

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Cancel"))
        {
            Close();
        }
        if (GUILayout.Button("Apply"))
        {
            Reimporter.OnReimported += OnReimported;
            maxProgress = Selection.objects.Length;
            for (int i = 0; i < Selection.objects.Length; i++)
            {
                progress = i;
                ToAnimation(Selection.objects[i]);
            }
            Reimporter.OnReimported -= OnReimported;
            Close();
        }
        //if (GUILayout.Button("Directories"))
        //{
        //    LogAllDirectoriesAndFiles();
        //}

        GUILayout.EndHorizontal();
    }

    public void OnReimported()
    {
        //Debug.Log("OnReimported".RTOlive());
    }

    public void ToAnimation(UnityEngine.Object obj)
    {

        string prefabName = "";
        string animationClipName = "";
        if (linkToPrefab)
        {
            string[] split = obj.name.Split(new char[] { '_' });
            if (split.Length > 1)
            {
                prefabName = split[0];
                animationClipName = split[split.Length - 1];
            }
        }
        //Debug.Log(prefabName);

        Image gifImage = Image.FromFile(AssetDatabase.GetAssetPath(obj));

        if (gifImage == null)
        {
            //Debug.Log("Invalid Path: " + AssetDatabase.GetAssetPath(obj));
            return;
        }

        FrameDimension dimension = new FrameDimension(gifImage.FrameDimensionsList[0]);
        int frameCount = gifImage.GetFrameCount(dimension);
        //Debug.Log("frameCount: " + frameCount);

        EditorUtility.DisplayProgressBar(progress + "/" + maxProgress + " Converting GIF To Frames", "This Step is fast!", .25f);
        Texture2D[] frames = GetGIFFrames(gifImage, dimension, frameCount, padding);
        //Debug.Log("Writing out frames: " + frames.Length);

        byte[] pngBytes = null;

        //EXPORTING OUT EACH FRAME individually
        //for (int i = 0; i < frames.Length; i++)
        //{
        //    pngBytes = frames[i].EncodeToPNG();
        //    string framePath = Application.dataPath + AssetDatabase.GetAssetPath(obj).Inset(6, 4 + obj.name.Length) + "/" + obj.name +"_"+ i + ".png";
        //    Debug.Log(framePath);
        //    File.WriteAllBytes(framePath, pngBytes);
        //}

        EditorUtility.DisplayProgressBar(progress + "/" + maxProgress + " Combining Frames", "Please Wait", .5f);
        Texture2D combinedTexture = CombineTextures(frames, padding);
        int frameLength = frames.Length;
        int width = frames[0].width;
        int height = frames[0].height;

        pngBytes = combinedTexture.EncodeToPNG();
        string assetPath = AssetDatabase.GetAssetPath(obj);
        assetPath = assetPath.Substring(0, assetPath.Length - 4);

        string path = Application.dataPath + assetPath.Substring(6, assetPath.Length - 6) + ".png";
        //Debug.Log(path);
        File.WriteAllBytes(path, pngBytes);

        AssetDatabase.ImportAsset(assetPath.Substring(0, assetPath.Length) + ".png", ImportAssetOptions.Default);
        if (combinedTexture == null)
        {
            combinedTexture = (Texture2D)AssetDatabase.LoadAssetAtPath(assetPath + ".png", typeof(Texture2D));
        }
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayProgressBar(progress + "/" + maxProgress + " Slicing Up Sprites", "Like Pie", .75f);
        float time = 0;
        while (combinedTexture == null && time < 10000)
        {
            time += Time.deltaTime + .001f;
        }

        Sprite[] sprites = SliceUKpSprite(combinedTexture,
            frameLength,
            assetPath + ".png",
            width,
            height,
            padding);

        EditorUtility.DisplayProgressBar(progress + "/" + maxProgress + " Making AnimationClip", "Every Single Byte", .9f);
        AnimationClip animationClip = new AnimationClip();
        animationClip.name = animationClipName;


        EditorCurveBinding binding = new EditorCurveBinding() { propertyName = "m_Sprite", path = "", type = typeof(SpriteRenderer) };


        List<ObjectReferenceKeyframe> objectReferenceKeyframe = new List<ObjectReferenceKeyframe>();
        for (int i = 0; i < sprites.Length; i++)
        {
            ObjectReferenceKeyframe orkf = new ObjectReferenceKeyframe() { value = sprites[i], time = (float)(i) * .1f };
            objectReferenceKeyframe.Add(orkf);
        }


        if (animationClip == null)
        {
            Debug.Log("NO Aniamtion Clip");
        }

        //AnimationClip loadedClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/" + animationClip.name + ".anim");
        EditorCurveBinding[] bindings = AnimationUtility.GetObjectReferenceCurveBindings(animationClip);
        for (int i = 0; i < bindings.Length; i++)
        {
            Debug.Log(bindings[i].path + "_AND_" + bindings[i].propertyName);
        }
        //AnimationUtility.GetObjectReferenceCurve(loadedClip)//

        if (animationClip == null)
        {
            //Debug.Log("No Such Clip Exists");
        }

        string savePath = assetPath + ".anim";

        //Debug.Log(obj.name);
        if (!string.IsNullOrEmpty(prefabName))
        {
            //Debug.Log("Searching For Prefab..." + prefabName);
            GameObject prefab = FindPrefabInProject(prefabName);
            if (prefab)
            {
                //Debug.Log("prefab found");
                Animator animator = prefab.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = prefab.AddComponent<Animator>();
                }
                AnimatorController animatorController = (AnimatorController)animator.runtimeAnimatorController;
                if (animatorController == null)
                {
                    animatorController = (AnimatorController)new AnimatorController();
                    string animatorSavePath = InsetFromEnd(AssetDatabase.GetAssetPath(prefab), prefab.name.Length + 7) + prefabName + ".controller";
                    //Debug.Log(animatorSavePath.RTBlue());
                    AssetDatabase.CreateAsset(animatorController, animatorSavePath);
                    animator.runtimeAnimatorController = (RuntimeAnimatorController)animatorController;
                }
                Motion motion = (Motion)animationClip as Motion;
                animatorController.AddLayer("Base Layer");
                animatorController.AddMotion(motion, 0);
                savePath = InsetFromEnd(AssetDatabase.GetAssetPath(prefab), prefab.name.Length + 7) + animationClipName + ".anim";

                AssetDatabase.MoveAsset(Inset(AssetDatabase.GetAssetPath(obj), 0, 4) + ".png", InsetFromEnd(AssetDatabase.GetAssetPath(prefab), prefab.name.Length + 7) + prefabName + ".png");

            }
        }

        AnimationUtility.SetObjectReferenceCurve(animationClip, binding, objectReferenceKeyframe.ToArray());//objectReferenceKeyframe.ToArray()
                                                                                                            //Debug.Log(savePath);
        AssetDatabase.CreateAsset(animationClip, savePath);
        EditorUtility.DisplayProgressBar(progress + "/" + maxProgress + " Saving...", "In Computer time 1 min Left (In Real Life 1 hr...)", .95f);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.ClearProgressBar();
    }


    public void Reference(EditorCurveBinding[] curves, AnimationClip animationClip)
    {

        for (int i = 0; i < curves.Length; i++)
        {
            EditorCurveBinding binding = (EditorCurveBinding)curves[i];
            AnimationCurve curve = AnimationUtility.GetEditorCurve(animationClip, binding);
            ObjectReferenceKeyframe[] objectReferenceCurve = null;

            if (curve != null)
            {
                AnimationUtility.SetEditorCurve(animationClip, binding, null);
            }
            else
            {
                objectReferenceCurve = AnimationUtility.GetObjectReferenceCurve(animationClip, binding);
                AnimationUtility.SetObjectReferenceCurve(animationClip, binding, null);
            }

            if (curve != null)
            {
                AnimationUtility.SetEditorCurve(animationClip, binding, curve);
            }
            else
            {
                AnimationUtility.SetObjectReferenceCurve(animationClip, binding, objectReferenceCurve);
            }

        }
    }

    public static GameObject FindPrefabInProject(string name)
    {
        string[] prefabPaths = FindAllProjectFolderFiles(".prefab", FindFileOptions.FilePathFromAssets);
        string[] prefabNames = FindAllProjectFolderFiles(".prefab", FindFileOptions.FileNameWithoutFiletype);
        int index = -1;
        for (int i = 0; i < prefabNames.Length; i++)
        {
            //Debug.Log(prefabNames[i]);
            if (prefabNames[i] == name)
            {
                index = i;
                i = prefabNames.Length;
            }
        }
        if (index == -1)
            return null;
        //Debug.Log("<color = red>" + prefabPaths[index] + "</color>");
        return (GameObject)AssetDatabase.LoadAssetAtPath(prefabPaths[index], typeof(GameObject));
    }

    public static UnityEngine.Object GetAsset(string path, string extension, System.Type assetType)
    {
        UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath(path, assetType);
        if (obj != null)
            return obj;
        return AssetDatabase.LoadAssetAtPath(path.Substring(Application.dataPath.Length - extension.Length, path.Length - Application.dataPath.Length - extension.Length), assetType);
    }

    public static UnityEngine.Object GetAsset(string path, System.Type assetType)
    {
        return GetAsset(path, "", assetType);
    }

    /// <summary>
    /// Returns all file names or paths of filetype in the Unity Project Folder
    /// </summary>
    /// <param name="fileType"></param>
    /// <param name="isNameNotPath"></param>
    /// <returns></returns>
    public static string[] FindAllProjectFolderFiles(string fileType, FindFileOptions findFileOptions)
    {
        DirectoryInfo directoryInfo = new DirectoryInfo(Application.dataPath);
        return FindFiles(fileType, directoryInfo, findFileOptions);
    }

    static string[] FindFiles(string fileType, DirectoryInfo directoryInfo, FindFileOptions findFileOptions)
    {
        List<string> matchingFiles = new List<string>();
        DirectoryInfo[] directoryInfos = directoryInfo.GetDirectories();
        for (int i = 0; i < directoryInfos.Length; i++)
        {
            matchingFiles.AddRange(FindFiles(fileType, directoryInfos[i], findFileOptions));
        }
        FileInfo[] files = directoryInfo.GetFiles();
        for (int i = 0; i < files.Length; i++)
        {
            if (files[i].Name.EndsWith(fileType))
            {
                switch (findFileOptions)
                {
                    case FindFileOptions.FullFilePath:
                        matchingFiles.Add(files[i].FullName);
                        break;
                    case FindFileOptions.FilePathWithoutFiletype:
                        matchingFiles.Add(files[i].DirectoryName);
                        break;
                    case FindFileOptions.FullFileName:
                        matchingFiles.Add(files[i].Name);
                        break;
                    case FindFileOptions.FileNameWithoutFiletype:
                        matchingFiles.Add(files[i].Name.Substring(0, files[i].Name.Length - fileType.Length));
                        break;
                    case FindFileOptions.FilePathFromAssets:
                        string noFileType = files[i].DirectoryName;
                        //Debug.Log(noFileType);
                        string[] split = noFileType.Split(new string[] { "Assets" }, StringSplitOptions.None);
                        string path = "Assets" + split[split.Length - 1] + "\\" + files[i].Name;
                        //Debug.Log(path);
                        matchingFiles.Add(path);// GET RID OF / AFTER ASSETS
                        break;

                    default:
                        break;
                }
            }
        }
        return matchingFiles.ToArray();
    }



    public void LogAllDirectoriesAndFiles()
    {
        Debug.Log(Application.dataPath);
        DirectoryInfo directoryInfo = new DirectoryInfo(Application.dataPath);
        FileInfo[] fileInfo = directoryInfo.GetFiles("*.*");
        DirectoryInfo[] directories = directoryInfo.GetDirectories();
        for (int i = 0; i < directories.Length; i++)
        {
            Debug.Log(directories[i]);
        }
        for (int i = 0; i < fileInfo.Length; i++)
        {
            Debug.Log(fileInfo[i]);
        }
    }



    /// <summary>
    /// 
    /// </summary>
    /// <param name="texture"></param>
    /// <param name="totalFrames"></param>
    /// <param name="path"></param>
    /// <param name="spriteWidth"></param>
    /// <param name="spriteHeight"></param>
    /// <param name="padding"></param>
    /// <returns></returns>
    public Sprite[] SliceUKpSprite(Texture2D texture, int totalFrames, string path, int spriteWidth, int spriteHeight, int padding)
    {
        //string path = AssetDatabase.GetAssetPath(path);

        spriteWidth -= padding;
        spriteHeight -= padding;
        //Debug.Log(path);
        TextureImporter texImporter = (TextureImporter)TextureImporter.GetAtPath(path);
        texImporter.textureType = TextureImporterType.Default;
        texImporter.spriteImportMode = SpriteImportMode.Multiple;
        texImporter.mipmapEnabled = false;
        texImporter.filterMode = FilterMode.Point;
        texImporter.npotScale = TextureImporterNPOTScale.None;
        texImporter.textureFormat = TextureImporterFormat.AutomaticTruecolor;

        int totalY = texture.height / spriteHeight;
        int totalX = texture.width / spriteWidth;

        List<SpriteMetaData> sprites = new List<SpriteMetaData>();
        int frameCount = 0;
        for (int y = 0; y < totalY; y++)
        {
            for (int x = 0; x < texture.width / spriteWidth; x++)
            {

                SpriteMetaData spriteMetaData = new SpriteMetaData();
                Vector2 pivot = new Vector2((x * spriteWidth) + (padding * (x + 1)) + spriteWidth / 2, (y * spriteHeight) + (padding * (y + 1)) + spriteHeight / 2);
                Rect rect = new Rect(x * spriteWidth + (padding * (x + 1)), y * spriteHeight + (padding * (y + 1)), spriteWidth, spriteHeight);
                spriteMetaData.rect = rect;
                spriteMetaData.pivot = pivot;
                spriteMetaData.name = "x_" + x + "_y_" + y;
                if (frameCount < totalFrames)
                    sprites.Add(spriteMetaData);
                frameCount++;
            }
        }
        texImporter.spritesheet = sprites.ToArray();
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Sprite[] spriteArray = AssetDatabase.LoadAllAssetsAtPath(path)
.OfType<Sprite>().ToArray();

        SortedDictionary<int, Sprite> sortedSprites = new SortedDictionary<int, Sprite>();
        for (int i = 0; i < spriteArray.Length; i++)
        {
            sortedSprites.Add(GetSpriteIndex(spriteArray[i], totalX), spriteArray[i]);
        }
        return sortedSprites.Values.ToArray();
    }

    public int GetSpriteIndex(Sprite sprite, int totalX)
    {
        string[] split = sprite.name.Split(new char[] { '_' });
        return int.Parse(split[1]) + (totalX * int.Parse(split[3]));
    }
      

    private static Texture2D[] GetGIFFrames(Image gifImage, FrameDimension dimension, int frameCount, int padding)
    {
        //Debug.Log("GetGIFFrames Start");
        List<Texture2D> gifFrames = new List<Texture2D>();
        //List<UnityEngine.Color> colors = new List<UnityEngine.Color>();
        for (int i = 0; i < frameCount; i++)
        {

            gifImage.SelectActiveFrame(dimension, i);
            var frame = new Bitmap(gifImage.Width, gifImage.Height);
            System.Drawing.Graphics.FromImage(frame).DrawImage(gifImage, Point.Empty);
            var frameTexture = new Texture2D(frame.Width + padding, frame.Height + padding);
            for (int x = 0; x < frameTexture.width; x++)
            {
                if (x < frame.Width)
                {
                    for (int y = 0; y < frameTexture.height; y++)
                    {
                        if (y < frame.Height)
                        {
                            System.Drawing.Color sourceColor = frame.GetPixel(x, y);
                            frameTexture.SetPixel(x, frame.Height - 1 - y, new Color32(sourceColor.R, sourceColor.G, sourceColor.B, sourceColor.A)); // for some reason, x is flipped
                                                                                                                                                     //colors.Add(new Color32(sourceColor.R, sourceColor.G, sourceColor.B, sourceColor.A));
                        }
                        else
                        {
                            frameTexture.SetPixel(x, frame.Height - 1 - y, new UnityEngine.Color(0, 0, 0, 0)); // for some reason, x is flipped
                                                                                                               //colors.Add(new UnityEngine.Color(0, 0, 0, 0));
                        }
                    }
                }
                else
                {
                    for (int y = 0; y < frameTexture.height; y++)
                    {
                        frameTexture.SetPixel(x, frame.Height - 1 - y, new UnityEngine.Color(0, 0, 0, 0)); // for some reason, x is flipped
                                                                                                           //colors.Add(new UnityEngine.Color(0, 0, 0, 0));
                    }
                }


            }


            //frameTexture.SetPixels(colors.ToArray());
            frameTexture.Apply();
            gifFrames.Add(frameTexture);
        }
        //Debug.Log("GetGIFFrames Success");
        return gifFrames.ToArray();
    }


    public static int SquareSizeCalc(int spots)
    {
        for (int i = 0; i < int.MaxValue; i++)
        {
            if (spots <= i * i)
            {
                return i;
            }
        }
        return -1;
    }

    /// <summary>
    /// These Textures should have padding already applied to themselves. This padding value should be for left and bottom border
    /// </summary>
    /// <param name="textures"></param>
    /// <param name="padding"></param>
    /// <returns></returns>
    public static Texture2D CombineTextures(Texture2D[] textures, int padding)
    {
        //Debug.Log("CombineTextures Starting");
        if (textures.Length == 0)
            return null;
        //Debug.Log("Combining Textures: " + textures.Length);
        int calcSize = SquareSizeCalc(textures.Length);
        //int squareSize = calcSize * calcSize;//calcSizetextures.Length.NextPowTwoValue();
        int side = calcSize;//squareSize / 2;

        int combinedWidth = (side * textures[0].width) + padding;
        int combinedHeight = (side * textures[0].height) + padding;
        //int noPaddingWidth = (side * textures[0].width);
        //int noPaddingHeight = (side * textures[0].height);
        int spriteWidth = textures[0].width;
        int spriteHeight = textures[0].height;

        //UnityEngine.Color[] combinedColors = new UnityEngine.Color[combinedWidth * combinedHeight];

        //Debug.Log("Width: " + combinedWidth + " Height: " + combinedHeight);

        Texture2D finalTexture = new Texture2D(combinedWidth, combinedHeight);
        //Debug.Log("Total Pixels: " + finalTexture.GetPixels().Length);
        //Debug.Log("Starting");
        int totalCount = 0;
        for (int y = 0; y < combinedHeight; y++)
        {
            int noPadY = y - padding;
            for (int x = 0; x < combinedWidth; x++)
            {
                int noPadX = x - padding;
                if (x < padding)
                {
                    finalTexture.SetPixel(x, y, new UnityEngine.Color(0, 0, 0, 0));
                    //combinedColors[totalCount] = new UnityEngine.Color(0, 0, 0, 0);
                }
                if (y < padding)
                {
                    finalTexture.SetPixel(x, y, new UnityEngine.Color(0, 0, 0, 0));
                    //combinedColors[totalCount] = new UnityEngine.Color(0, 0, 0, 0);
                }
                else
                {
                    int yIndex = noPadY / spriteHeight;
                    int xIndex = noPadX / spriteWidth;


                    int textureIndex = (yIndex * side) + xIndex;
                    //Debug.Log("Texture Index: ".RTOrange() + textureIndex);
                    //if (!textures.ToList().IsIndexInRange(textureIndex+3))
                    //{
                    //    Debug.Log("LastFrames".RTPurple());
                    //    Debug.Log("squareSize: ".RTOrange() + squareSize);
                    //    Debug.Log("Texture Index: ".RTOrange() + textureIndex);
                    //    Debug.Log("Texture Length: ".RTOrange() + textures.Length);
                    //    Debug.Log("yIndex: ".RTOrange() + yIndex);
                    //    Debug.Log("xIndex: ".RTOrange() + xIndex);
                    //    Debug.Log("combinedWidth: ".RTOrange() + combinedWidth);
                    //    Debug.Log("combinedHeight: ".RTOrange() + combinedHeight);
                    //}
                    if (textureIndex >= 0 && textureIndex < textures.Length)
                    {
                        Texture2D currentTexture = textures[textureIndex];
                        UnityEngine.Color sourceColor = currentTexture.GetPixel(noPadX % spriteWidth, noPadY % spriteHeight);
                        finalTexture.SetPixel(x, y, sourceColor);
                    }
                    else
                    {
                        finalTexture.SetPixel(x, y, new UnityEngine.Color(0, 0, 0, 0));//This handles cases where you are taking 7 of 9 total texture squares
                    }

                    //combinedColors[totalCount] = sourceColor;
                }
                totalCount++;
            }
        }

        //UnityEngine.Color[] aBaseTexturePixels = textureOne.GetPixels();
        //UnityEngine.Color[] aCopyTexturePixels = textureTwo.GetPixels();
        //UnityEngine.Color[] aColorList = new UnityEngine.Color[aBaseTexturePixels.Length];

        //int aPixelLength = aBaseTexturePixels.Length;

        //for (int p = 0; p < aPixelLength; p++)
        //{
        //    aColorList[p] = UnityEngine.Color.Lerp(aBaseTexturePixels[p], aCopyTexturePixels[p], aCopyTexturePixels[p].a);
        //}


        //finalTexture.SetPixels(combinedColors);
        finalTexture.Apply(false);
        if (finalTexture != null)
        {
            //Debug.Log("CombineTextures Success + " + finalTexture);    
        }
        else
        {
            //Debug.Log("Failed to make texture");
        }

        return finalTexture;
    }

    public static int CalcWidth(Texture2D[] textures)
    {
        return -1;
    }

    public static int CalcHeight(Texture2D[] textures)
    {
        return -1;
    }
}

public enum FindFileOptions
{
    FullFilePath,
    FilePathWithoutFiletype,
    FullFileName,
    FileNameWithoutFiletype,
    FilePathFromAssets
}
