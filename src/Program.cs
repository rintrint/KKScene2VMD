using MessagePack;
using System.Globalization;
using System.Numerics;
using System.Xml;

[MessagePackObject]
public class PluginData
{
    [Key(0)]
    public int version { get; set; }
    [Key(1)]
    public Dictionary<string, object> data { get; set; } = new Dictionary<string, object>();
}

public class Program
{
    public static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("Koikatsu Scene Parser v8.2 (Final Version)");
        Console.WriteLine("====================================================");

        if (args.Length == 0) { Console.WriteLine("用法: KKScene2VMD.exe \"scene.png\""); return; }
        string filePath = args[0];
        if (!File.Exists(filePath)) { Console.WriteLine($"檔案不存在: {filePath}"); return; }

        try
        {
            string vmdPath = Path.ChangeExtension(filePath, ".vmd");
            ParseScene(filePath, vmdPath);
        }
        catch (Exception ex) { Console.WriteLine($"錯誤: {ex.Message}\n{ex.StackTrace}"); }
    }

    static void ParseScene(string filePath, string vmdPath)
    {
        using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
        using (BinaryReader reader = new BinaryReader(fs))
        {
            Console.WriteLine($"檔案大小: {fs.Length} bytes");
            long pngSize = ParserUtils.GetPngSize(fs);
            Console.WriteLine($"PNG 大小: {pngSize} bytes");
            fs.Seek(pngSize, SeekOrigin.Begin);

            string version = ParserUtils.ReadNetString(reader);
            Console.WriteLine($"場景版本: '{version}'");
            int objectCount = reader.ReadInt32();
            Console.WriteLine($"場景物件數量: {objectCount}");

            for (int i = 0; i < objectCount; i++)
            {
                Console.WriteLine($"\n=== 解析頂層物件 {i + 1} / {objectCount} ===");
                int objectKey = reader.ReadInt32();
                if (!ParseAnyObject(reader, new Version(version), 0, objectKey))
                {
                    Console.WriteLine($"物件 {i + 1} 解析失敗或未完全支援，終止解析。");
                    break;
                }
            }
            Console.WriteLine("\n====================================================");
            Console.WriteLine($"物件列表解析完畢。");
            Console.WriteLine($"當前檔案指標位置: {reader.BaseStream.Position}");

            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                ParseSceneData(reader);
            }

            if (reader.BaseStream.Position < reader.BaseStream.Length)
            {
                ParseExtendedData(reader, vmdPath);
            }

            Console.WriteLine("\n====================================================");
            Console.WriteLine("場景檔案解析完畢。");
        }
    }

    static bool ParseAnyObject(BinaryReader reader, Version dataVersion, int depth, int key)
    {
        string indent = new string(' ', depth * 2);
        int objectKind = reader.ReadInt32();
        int internalKey = reader.ReadInt32();
        Console.WriteLine($"{indent}物件起始位置: {reader.BaseStream.Position - 8}");
        Console.WriteLine($"{indent}Key: {key}, Kind: {objectKind} ({ParserUtils.GetKindName(objectKind)}), 內部 Key: {internalKey}");
        ParserUtils.LoadObjectInfoBaseData(reader, depth);
        switch (objectKind)
        {
            case 0: return LoadCharacterData(reader, dataVersion, depth);
            case 1: return LoadItemData(reader, dataVersion, depth);
            case 3: return LoadFolderData(reader, dataVersion, depth);
            default:
                Console.WriteLine($"{indent}[錯誤] 偵測到未支援的物件類型 (Kind={objectKind})。");
                return false;
        }
    }

    static bool LoadFolderData(BinaryReader reader, Version dataVersion, int depth)
    {
        string indent = new string(' ', depth * 2);
        Console.WriteLine($"{indent}--- 開始解析 OIFolderInfo (資料夾) ---");
        string name = ParserUtils.ReadNetString(reader);
        Console.WriteLine($"{indent}[OIFolderInfo] 資料夾名稱: '{name}'");
        int childCount = reader.ReadInt32();
        Console.WriteLine($"{indent}[OIFolderInfo] 子物件數量: {childCount}");
        for (int i = 0; i < childCount; i++)
        {
            if (!ParseAnyObject(reader, dataVersion, depth + 1, -1)) return false;
        }
        return true;
    }

    static bool LoadItemData(BinaryReader reader, Version dataVersion, int depth)
    {
        string indent = new string(' ', depth * 2);
        Console.WriteLine($"{indent}--- 開始解析 OIItemInfo (道具) ---");
        Console.WriteLine($"{indent}[OIItemInfo] ID: Group={reader.ReadInt32()}, Category={reader.ReadInt32()}, No={reader.ReadInt32()}");
        Console.WriteLine($"{indent}[OIItemInfo] 動畫速度: {reader.ReadSingle():F2}");
        for (int i = 0; i < 8; i++) { ParserUtils.ReadNetString(reader); }
        for (int i = 0; i < 3; i++) { ParserUtils.LoadPatternInfo(reader); }
        Console.WriteLine($"{indent}[OIItemInfo] Alpha: {reader.ReadSingle():F2}");
        ParserUtils.ReadNetString(reader);
        Console.WriteLine($"{indent}[OIItemInfo] 線條寬度: {reader.ReadSingle():F2}");
        ParserUtils.ReadNetString(reader);
        Console.WriteLine($"{indent}[OIItemInfo] 自發光強度: {reader.ReadSingle():F2}");
        Console.WriteLine($"{indent}[OIItemInfo] 光照抵消: {reader.ReadSingle():F2}");
        ParserUtils.LoadPatternInfo(reader);
        Console.WriteLine($"{indent}[OIItemInfo] FK啟用: {reader.ReadBoolean()}");
        int bonesCount = reader.ReadInt32();
        for (int i = 0; i < bonesCount; i++) { ParserUtils.ReadNetString(reader); ParserUtils.LoadOIBoneInfo(reader); }
        Console.WriteLine($"{indent}[OIItemInfo] 動態骨骼啟用: {reader.ReadBoolean()}");
        Console.WriteLine($"{indent}[OIItemInfo] 動畫標準化時間: {reader.ReadSingle():F3}");
        int childCount = reader.ReadInt32();
        Console.WriteLine($"{indent}[OIItemInfo] 子物件數量: {childCount}");
        for (int i = 0; i < childCount; i++)
        {
            if (!ParseAnyObject(reader, dataVersion, depth + 1, -1)) return false;
        }
        return true;
    }

    static bool LoadCharacterData(BinaryReader reader, Version dataVersion, int depth)
    {
        string indent = new string(' ', depth * 2);
        Console.WriteLine($"{indent}--- 開始解析 OICharInfo (角色) ---");
        Console.WriteLine($"{indent}[OICharInfo] 性別: {reader.ReadInt32()}");
        if (!ParserUtils.LoadAndSkipCharacterFile(reader)) return false;
        ParserUtils.LoadDictionary(reader, ParserUtils.LoadOIBoneInfo);
        ParserUtils.LoadDictionary(reader, ParserUtils.LoadOIBoneInfo);
        int childParentCount = reader.ReadInt32();
        for (int i = 0; i < childParentCount; i++)
        {
            int parentKey = reader.ReadInt32();
            int childListCount = reader.ReadInt32();
            Console.WriteLine($"{indent}  - 配件掛點 Key: {parentKey}, 子物件數量: {childListCount}");
            for (int j = 0; j < childListCount; j++)
            {
                if (!ParseAnyObject(reader, dataVersion, depth + 1, -1)) return false;
            }
        }
        reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt32(); reader.ReadInt32();
        reader.ReadSingle(); reader.ReadBytes(5); reader.ReadSingle(); reader.ReadBoolean();
        ParserUtils.LoadOIBoneInfo(reader);
        reader.ReadBoolean(); reader.ReadBytes(5); reader.ReadBoolean(); reader.ReadBytes(7); reader.ReadBytes(8);
        reader.ReadSingle(); reader.ReadSingle(); reader.ReadBoolean(); reader.ReadBoolean();
        ParserUtils.LoadVoiceCtrl(reader);
        reader.ReadBoolean(); reader.ReadSingle(); reader.ReadBoolean(); ParserUtils.ReadNetString(reader);
        reader.ReadSingle(); reader.ReadSingle();
        int neckByteDataLen = reader.ReadInt32(); if (neckByteDataLen > 0) reader.ReadBytes(neckByteDataLen);
        int eyesByteDataLen = reader.ReadInt32(); if (eyesByteDataLen > 0) reader.ReadBytes(eyesByteDataLen);
        reader.ReadSingle();
        ParserUtils.LoadIntIntDictionary(reader);
        ParserUtils.LoadIntIntDictionary(reader);
        return true;
    }

    static void ParseSceneData(BinaryReader reader)
    {
        Console.WriteLine("\n--- 開始解析場景數據 ---");
        Console.WriteLine($"地圖 ID: {reader.ReadInt32()}");
        ParserUtils.LoadChangeAmount(reader, "  ");
        Console.WriteLine($"陽光類型: {reader.ReadInt32()}");
        Console.WriteLine($"地圖選項: {reader.ReadBoolean()}");
        Console.WriteLine($"濾鏡(ACE)編號: {reader.ReadInt32()}");
        Console.WriteLine($"濾鏡混合度: {reader.ReadSingle():F2}");
        Console.WriteLine($"環境光遮蔽(AOE)啟用: {reader.ReadBoolean()}");
        ParserUtils.ReadNetString(reader);
        Console.WriteLine($"AOE半徑: {reader.ReadSingle():F2}");
        Console.WriteLine($"泛光(Bloom)啟用: {reader.ReadBoolean()}");
        Console.WriteLine($"泛光強度: {reader.ReadSingle():F2}");
        Console.WriteLine($"泛光模糊度: {reader.ReadSingle():F2}");
        Console.WriteLine($"泛光閾值: {reader.ReadSingle():F2}");
        Console.WriteLine($"景深(Depth)啟用: {reader.ReadBoolean()}");
        Console.WriteLine($"景深焦距: {reader.ReadSingle():F2}");
        Console.WriteLine($"景深光圈: {reader.ReadSingle():F2}");
        Console.WriteLine($"暗角(Vignette)啟用: {reader.ReadBoolean()}");
        Console.WriteLine($"霧效(Fog)啟用: {reader.ReadBoolean()}");
        ParserUtils.ReadNetString(reader);
        Console.WriteLine($"霧效高度: {reader.ReadSingle():F2}");
        Console.WriteLine($"霧效起始距離: {reader.ReadSingle():F2}");
        Console.WriteLine($"體積光(SunShafts)啟用: {reader.ReadBoolean()}");
        ParserUtils.ReadNetString(reader);
        ParserUtils.ReadNetString(reader);
        Console.WriteLine($"投射體積光的光源Key: {reader.ReadInt32()}");
        Console.WriteLine($"陰影啟用: {reader.ReadBoolean()}");
        Console.WriteLine($"臉部法線: {reader.ReadBoolean()}");
        Console.WriteLine($"臉部陰影: {reader.ReadBoolean()}");
        Console.WriteLine($"線條顏色G: {reader.ReadSingle():F2}");
        ParserUtils.ReadNetString(reader);
        Console.WriteLine($"線條寬度G: {reader.ReadSingle():F2}");
        Console.WriteLine($"RampG: {reader.ReadInt32()}");
        Console.WriteLine($"環境陰影G: {reader.ReadSingle():F2}");
        Console.WriteLine("\n--- 開始解析相機、光照、BGM等數據 ---");
        ParserUtils.LoadCameraData(reader, "主相機數據 (cameraSaveData)");
        for (int i = 0; i < 10; i++)
        {
            ParserUtils.LoadCameraData(reader, $"相機書籤 {i}");
        }
        ParserUtils.LoadCharaLight(reader);
        ParserUtils.LoadMapLight(reader);
        ParserUtils.LoadSoundControl(reader, "BGM控制器", hasFileName: false);
        ParserUtils.LoadSoundControl(reader, "環境音控制器", hasFileName: false);
        ParserUtils.LoadSoundControl(reader, "外部音效控制器", hasFileName: true);
        Console.WriteLine("\n--- 其他場景設定 ---");
        Console.WriteLine($"背景圖片: '{ParserUtils.ReadNetString(reader)}'");
        Console.WriteLine($"前景框架: '{ParserUtils.ReadNetString(reader)}'");
        if (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            string endTag = ParserUtils.ReadNetString(reader);
            Console.WriteLine($"檔案結束標記: '{endTag}'");
        }
    }

    static void ParseExtendedData(BinaryReader reader, string vmdPath)
    {
        Console.WriteLine("\n--- 開始解析擴展插件數據 (ExtensibleSaveFormat) ---");
        try
        {
            if (reader.BaseStream.Position >= reader.BaseStream.Length)
            {
                Console.WriteLine("未發現擴展數據。");
                return;
            }

            const string ExpectedMarker = "KKEx";
            string marker = ParserUtils.ReadNetString(reader);

            if (marker != ExpectedMarker)
            {
                Console.WriteLine($"錯誤: 擴展數據標記不符。預期: '{ExpectedMarker}', 實際: '{marker}'。解析終止。");
                return;
            }
            Console.WriteLine($"成功識別擴展數據標記: '{marker}'");

            int esVersion = reader.ReadInt32();
            int dataLength = reader.ReadInt32();
            Console.WriteLine($"擴展數據版本: {esVersion}, 數據塊長度: {dataLength} bytes");

            byte[] allPluginBytes = reader.ReadBytes(dataLength);
            var allPluginsData = MessagePackSerializer.Deserialize<Dictionary<string, PluginData>>(allPluginBytes);

            Console.WriteLine($"\n在擴展數據中發現 {allPluginsData.Count} 個插件的數據，列表如下：");
            int pluginIndex = 1;
            foreach (var pluginId in allPluginsData.Keys)
            {
                Console.WriteLine($"  {pluginIndex++}. {pluginId}");
            }

            if (allPluginsData.TryGetValue("timeline", out var timelinePluginData))
            {
                Console.WriteLine("\n--- 成功找到 Timeline 插件數據 ---");
                Console.WriteLine($"  Timeline數據版本: {timelinePluginData.version}");

                if (timelinePluginData.data.TryGetValue("sceneInfo", out var sceneInfoObject) && sceneInfoObject is string xmlString)
                {
                    Console.WriteLine("  成功提取 Timeline XML 動畫數據，結構如下：\n");
                    XmlDocument xmlDoc = new XmlDocument();
                    xmlDoc.LoadXml(xmlString);

                    if (xmlDoc.DocumentElement != null)
                    {
                        ParserUtils.PrintXmlNode(xmlDoc.DocumentElement, 2);

                        // *** VMD EXPORT CALL ***
                        Console.WriteLine("\n--- 開始導出 VMD 動畫數據 ---");
                        try
                        {
                            GenerateAndExportVmd(xmlDoc, vmdPath);
                            Console.WriteLine($"成功導出 VMD 檔案: {vmdPath}");
                        }
                        catch (Exception vmdEx)
                        {
                            Console.WriteLine($"導出 VMD 時發生嚴重錯誤: {vmdEx.Message}\n{vmdEx.StackTrace}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("  未在 Timeline 數據中找到 'sceneInfo' XML 字串。");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"解析擴展數據時發生錯誤: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
        }
    }

    static void GenerateAndExportVmd(XmlDocument xmlDoc, string vmdPath)
    {
        var framesByObject = new Dictionary<string, Dictionary<string, SortedDictionary<uint, VmdMotionFrame>>>();
        const float FPS = 30.0f;

        XmlNodeList? trackNodes = xmlDoc.SelectNodes("/root/interpolable");
        if (trackNodes == null || trackNodes.Count == 0)
        {
            Console.WriteLine("警告: Timeline XML 中未找到任何動畫軌道(interpolable)。");
            return;
        }

        foreach (XmlNode trackNode in trackNodes)
        {
            string? objectIndex = trackNode.Attributes?["objectIndex"]?.Value;
            // 如果這條軌道沒有指定物件，或者不包含骨骼資訊，則跳過
            if (string.IsNullOrEmpty(objectIndex)) continue;

            string? id = trackNode.Attributes?["id"]?.Value;
            string? owner = trackNode.Attributes?["owner"]?.Value;

            // 只處理跟骨骼位置和旋轉相關的軌道
            bool isBoneTrack = id == "guideObjectPos" || id == "guideObjectRot" || (owner == "KKPE" && id == "boneRot");
            if (!isBoneTrack) continue;

            string? kkBoneName = owner == "KKPE"
                ? trackNode.Attributes?["parameter"]?.Value
                : trackNode.Attributes?["guideObjectPath"]?.Value;

            if (kkBoneName is null || !BoneMapper.TryGetMmdBone(kkBoneName, out string? mmdBoneName)) continue;

            // --- 將幀數據存入對應 objectIndex 的巢狀字典中 ---
            // 確保該 objectIndex 的字典存在
            if (!framesByObject.ContainsKey(objectIndex))
            {
                framesByObject[objectIndex] = new Dictionary<string, SortedDictionary<uint, VmdMotionFrame>>();
            }
            var objectFrames = framesByObject[objectIndex];

            // 確保該骨骼的 SortedDictionary 存在
            if (!objectFrames.ContainsKey(mmdBoneName))
            {
                objectFrames[mmdBoneName] = new SortedDictionary<uint, VmdMotionFrame>();
            }
            var boneFrames = objectFrames[mmdBoneName];

            var keyframeNodes = trackNode.SelectNodes("keyframe");
            if (keyframeNodes is null) continue;

            foreach (XmlNode keyframeNode in keyframeNodes)
            {
                try
                {
                    string? timeValue = keyframeNode.Attributes?["time"]?.Value;
                    if (timeValue is null) continue;

                    float time = float.Parse(timeValue, CultureInfo.InvariantCulture);
                    uint frameNum = (uint)Math.Round(time * FPS);

                    if (!boneFrames.ContainsKey(frameNum)) boneFrames[frameNum] = new VmdMotionFrame(mmdBoneName, frameNum);
                    var frame = boneFrames[frameNum];

                    if (id == "guideObjectPos")
                    {
                        float x = float.Parse(keyframeNode.Attributes?["valueX"]?.Value!, CultureInfo.InvariantCulture);
                        float y = float.Parse(keyframeNode.Attributes?["valueY"]?.Value!, CultureInfo.InvariantCulture);
                        float z = float.Parse(keyframeNode.Attributes?["valueZ"]?.Value!, CultureInfo.InvariantCulture);
                        x *= 12.5f;
                        y *= 12.5f;
                        z *= 12.5f;
                        frame.Position = new Vector3(-x, y, -z); // 坐標系轉換
                    }
                    else if (id == "guideObjectRot" || (owner == "KKPE" && id == "boneRot"))
                    {
                        float x = float.Parse(keyframeNode.Attributes?["valueX"]?.Value!, CultureInfo.InvariantCulture);
                        float y = float.Parse(keyframeNode.Attributes?["valueY"]?.Value!, CultureInfo.InvariantCulture);
                        float z = float.Parse(keyframeNode.Attributes?["valueZ"]?.Value!, CultureInfo.InvariantCulture);
                        float w = float.Parse(keyframeNode.Attributes?["valueW"]?.Value!, CultureInfo.InvariantCulture);
                        frame.Rotation = new Quaternion(-x, y, -z, w); // 坐標系轉換
                    }
                }
                catch (Exception)
                {
                    /* 忽略任何解析失敗 (缺少value或格式錯誤) 的keyframe */
                }
            }
        }

        if (framesByObject.Count == 0)
        {
            Console.WriteLine("警告: 未能從XML中解析出任何有效的骨骼關鍵幀。");
            return;
        }

        // --- 遍歷每個物件並單獨導出 VMD 檔案 ---
        string baseName = Path.GetFileNameWithoutExtension(vmdPath); // e.g., "scene"
        string? directory = Path.GetDirectoryName(vmdPath); // e.g., "C:\path\to"

        foreach (var objectEntry in framesByObject)
        {
            string objectIndex = objectEntry.Key;
            var allFramesForObject = objectEntry.Value;

            // 為每個物件構造新的檔名, e.g., "C:\path\to\scene_0.vmd"
            string newVmdPath = Path.Combine(directory ?? "", $"{baseName}_{objectIndex}.vmd");

            List<VmdMotionFrame> flattenedFrames = new List<VmdMotionFrame>();
            foreach (var boneTimeline in allFramesForObject.Values)
            {
                Vector3? lastPos = null;
                Quaternion? lastRot = null;

                bool hasAnyPosition = boneTimeline.Values.Any(f => f.Position.HasValue);
                bool hasAnyRotation = boneTimeline.Values.Any(f => f.Rotation.HasValue);

                if (!hasAnyPosition && !hasAnyRotation) continue;

                foreach (var frame in boneTimeline.Values)
                {
                    if (frame.Position == null)
                    {
                        frame.Position = hasAnyPosition ? lastPos : Vector3.Zero;
                    }
                    else
                    {
                        lastPos = frame.Position;
                    }

                    if (frame.Rotation == null)
                    {
                        frame.Rotation = hasAnyRotation ? lastRot : Quaternion.Identity;
                    }
                    else
                    {
                        lastRot = frame.Rotation;
                    }

                    if (frame.Position == null) frame.Position = Vector3.Zero;
                    if (frame.Rotation == null) frame.Rotation = Quaternion.Identity;

                    flattenedFrames.Add(frame);
                }
            }

            if (flattenedFrames.Count == 0)
            {
                Console.WriteLine($"警告: 物件 {objectIndex} 未解析出任何有效的骨骼關鍵幀，已跳過。");
                continue;
            }

            VmdExporter exporter = new VmdExporter();
            exporter.MotionFrames.AddRange(flattenedFrames.OrderBy(f => f.FrameNumber).ThenBy(f => f.BoneName));
            exporter.Write(newVmdPath);
            Console.WriteLine($"成功為物件 {objectIndex} 導出 VMD 檔案: {newVmdPath}");
        }
    }
}
