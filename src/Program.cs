using System;
using System.IO;

public class Program
{
    public static void Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;
        Console.WriteLine("Koikatsu Scene Parser v8.1 (Full Log & Final Logic)");
        Console.WriteLine("====================================================");

        if (args.Length == 0) { Console.WriteLine("用法: program.exe \"scene.png\""); return; }
        string filePath = args[0];
        if (!File.Exists(filePath)) { Console.WriteLine($"檔案不存在: {filePath}"); return; }

        try { ParseScene(filePath); }
        catch (Exception ex) { Console.WriteLine($"錯誤: {ex.Message}\n{ex.StackTrace}"); }
    }

    static void ParseScene(string filePath)
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

            Console.WriteLine("\n====================================================");
            Console.WriteLine("場景檔案解析完畢。");
        }
    }

    // 統一的物件解析函式
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

        // ===== 以下為新增的解析部分 =====
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

        // 檢查檔案結尾標記
        if (reader.BaseStream.Position < reader.BaseStream.Length)
        {
            string endTag = ParserUtils.ReadNetString(reader);
            Console.WriteLine($"檔案結束標記: '{endTag}'");
        }
    }
}