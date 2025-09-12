using System.Xml;

public enum UnityLightType { Spot, Directional, Point, Area }
public enum RepeatMode { None, All, Select }

public static class ParserUtils
{
    public static string ReadNetString(BinaryReader reader) => reader.ReadString();

    public static string GetKindName(int kind) => kind switch
    {
        0 => "角色",
        1 => "道具",
        2 => "光源",
        3 => "資料夾",
        4 => "路徑",
        5 => "相機",
        6 => "路徑點",
        _ => "未知"
    };

    public static void LoadChangeAmount(BinaryReader reader, string indent = "")
    {
        Console.WriteLine($"{indent}位置: ({reader.ReadSingle():F2}, {reader.ReadSingle():F2}, {reader.ReadSingle():F2})");
        Console.WriteLine($"{indent}旋轉: ({reader.ReadSingle():F2}, {reader.ReadSingle():F2}, {reader.ReadSingle():F2})");
        Console.WriteLine($"{indent}縮放: ({reader.ReadSingle():F2}, {reader.ReadSingle():F2}, {reader.ReadSingle():F2})");
    }

    public static void LoadObjectInfoBaseData(BinaryReader reader, int depth)
    {
        string indent = new string(' ', depth * 2);
        Console.WriteLine($"{indent}[ObjectInfo] 基礎數據:");
        LoadChangeAmount(reader, indent + "  ");
        Console.WriteLine($"{indent}  TreeState: {reader.ReadInt32()}, Visible: {reader.ReadBoolean()}");
    }

    public static void LoadPatternInfo(BinaryReader reader)
    {
        reader.ReadInt32(); ReadNetString(reader); reader.ReadBoolean(); ReadNetString(reader); reader.ReadSingle();
    }

    public static void LoadVoiceCtrl(BinaryReader reader)
    {
        int c = reader.ReadInt32(); if (c > 0) reader.ReadBytes(c * 12); reader.ReadInt32();
    }

    public static void LoadDictionary(BinaryReader reader, Action<BinaryReader> act)
    {
        int c = reader.ReadInt32(); for (int i = 0; i < c; i++) { reader.ReadInt32(); act(reader); }
    }

    public static void LoadIntIntDictionary(BinaryReader reader)
    {
        int c = reader.ReadInt32(); if (c > 0) reader.ReadBytes(c * 8);
    }

    public static void LoadOIBoneInfo(BinaryReader reader)
    {
        reader.ReadInt32(); reader.ReadBytes(36);
    }

    public static bool LoadAndSkipCharacterFile(BinaryReader reader)
    {
        reader.ReadInt32(); ReadNetString(reader); ReadNetString(reader);
        int f = reader.ReadInt32(); if (f > 0) reader.ReadBytes(f);
        int b = reader.ReadInt32(); if (b > 0) reader.ReadBytes(b);
        long d = reader.ReadInt64(); if (d > 0) reader.ReadBytes((int)d);
        return true;
    }

    public static string ReadColor(BinaryReader reader)
    {
        float r = reader.ReadSingle();
        float g = reader.ReadSingle();
        float b = reader.ReadSingle();
        float a = reader.ReadSingle();
        return $"R:{r:F2} G:{g:F2} B:{b:F2} A:{a:F2}";
    }

    public static void LoadCameraData(BinaryReader reader, string name)
    {
        Console.WriteLine($"--- {name} ---");
        int version = reader.ReadInt32();
        Console.WriteLine($"  數據版本: {version}");
        float posX = reader.ReadSingle(); float posY = reader.ReadSingle(); float posZ = reader.ReadSingle();
        Console.WriteLine($"  位置: ({posX:F2}, {posY:F2}, {posZ:F2})");
        float rotX = reader.ReadSingle(); float rotY = reader.ReadSingle(); float rotZ = reader.ReadSingle();
        Console.WriteLine($"  旋轉: ({rotX:F2}, {rotY:F2}, {rotZ:F2})");
        float distX = 0, distY = 0, distZ = 0;
        if (version == 1) { reader.ReadSingle(); }
        else { distX = reader.ReadSingle(); distY = reader.ReadSingle(); distZ = reader.ReadSingle(); }
        Console.WriteLine($"  距離向量: ({distX:F2}, {distY:F2}, {distZ:F2})");
        float fov = reader.ReadSingle();
        Console.WriteLine($"  視野(FOV): {fov:F2}");
    }

    public static void LoadCharaLight(BinaryReader reader)
    {
        Console.WriteLine("--- 角色光照 ---");
        Console.WriteLine($"  顏色(Json): {ReadNetString(reader)}");
        Console.WriteLine($"  強度: {reader.ReadSingle():F2}");
        Console.WriteLine($"  X軸旋轉: {reader.ReadSingle():F2}");
        Console.WriteLine($"  Y軸旋轉: {reader.ReadSingle():F2}");
        Console.WriteLine($"  啟用陰影: {reader.ReadBoolean()}");
    }

    public static void LoadMapLight(BinaryReader reader)
    {
        Console.WriteLine("--- 地圖光照 ---");
        Console.WriteLine($"  顏色(Json): {ReadNetString(reader)}");
        Console.WriteLine($"  強度: {reader.ReadSingle():F2}");
        Console.WriteLine($"  X軸旋轉: {reader.ReadSingle():F2}");
        Console.WriteLine($"  Y軸旋轉: {reader.ReadSingle():F2}");
        Console.WriteLine($"  啟用陰影: {reader.ReadBoolean()}");
        Console.WriteLine($"  光源類型: {(UnityLightType)reader.ReadInt32()}");
    }

    public static void LoadSoundControl(BinaryReader reader, string name, bool hasFileName)
    {
        Console.WriteLine($"--- {name} ---");
        Console.WriteLine($"  播放中: {reader.ReadBoolean()}");
        Console.WriteLine($"  重複模式: {(RepeatMode)reader.ReadInt32()}");
        if (hasFileName)
        {
            Console.WriteLine($"  檔案名稱: '{ReadNetString(reader)}'");
        }
        else
        {
            Console.WriteLine($"  編號: {reader.ReadInt32()}");
        }
    }

    public static void PrintXmlNode(XmlNode node, int indentLevel)
    {
        string indent = new string(' ', indentLevel * 2);

        var attributes = node.Attributes?.Cast<XmlAttribute>()
                                        .Select(attr => $"{attr.Name}=\"{attr.Value}\"")
                                        ?? Enumerable.Empty<string>();

        string attributeString = string.Join(" ", attributes);

        if (!node.HasChildNodes)
        {
            // 如果節點沒有子節點，則印出一個自我關閉的標籤。
            Console.WriteLine($"{indent}<{node.Name} {attributeString} />");
        }
        else
        {
            // 如果有子節點，則印出開頭標籤。
            Console.WriteLine($"{indent}<{node.Name} {attributeString}>");

            foreach (XmlNode child in node.ChildNodes)
            {
                if (child.NodeType == XmlNodeType.Element)
                {
                    // 遞迴呼叫以處理子元素。
                    PrintXmlNode(child, indentLevel + 1);
                }
                else if (child.NodeType == XmlNodeType.Text && !string.IsNullOrWhiteSpace(child.Value))
                {
                    string textIndent = new string(' ', (indentLevel + 1) * 2);
                    Console.WriteLine($"{textIndent}{child.Value.Trim()}");
                }
                else if (child.NodeType == XmlNodeType.Comment)
                {
                    // 同時處理註解節點。
                    string commentIndent = new string(' ', (indentLevel + 1) * 2);
                    Console.WriteLine($"{commentIndent}");
                }
            }

            // 處理完所有子節點後，印出結尾標籤。
            Console.WriteLine($"{indent}</{node.Name}>");
        }
    }

    public static void DumpHexData(BinaryReader reader, int length, string description = "數據")
    {
        long currentPos = reader.BaseStream.Position;
        if (currentPos >= reader.BaseStream.Length) { Console.WriteLine($"{description}: 已達檔案結尾。"); return; }
        try
        {
            byte[] data = reader.ReadBytes(Math.Min(length, (int)(reader.BaseStream.Length - currentPos)));
            Console.WriteLine($"{description} (位置 {currentPos}, 長度 {data.Length}):");
            for (int i = 0; i < data.Length; i += 16)
            {
                string hex = BitConverter.ToString(data, i, Math.Min(16, data.Length - i)).Replace("-", " ");
                string ascii = "";
                for (int j = 0; j < 16 && i + j < data.Length; j++) { char c = (char)data[i + j]; ascii += char.IsControl(c) ? '.' : c; }
                Console.WriteLine($"  {currentPos + i:X8}: {hex.PadRight(48)} {ascii}");
            }
            Console.WriteLine();
        }
        finally { reader.BaseStream.Position = currentPos; }
    }

    public static long GetPngSize(Stream stream)
    {
        long originalPos = stream.Position;
        try
        {
            if (stream.Length - originalPos < 8) return 0;
            byte[] pngSignature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
            byte[] buffer = new byte[8];
            stream.Read(buffer, 0, 8);
            for (int i = 0; i < 8; i++) if (buffer[i] != pngSignature[i]) return 0;

            while (stream.Position < stream.Length)
            {
                if (stream.Length - stream.Position < 8) break;
                byte[] lengthBytes = new byte[4];
                stream.Read(lengthBytes, 0, 4);
                Array.Reverse(lengthBytes);
                int chunkLength = BitConverter.ToInt32(lengthBytes, 0);

                byte[] typeBytes = new byte[4];
                stream.Read(typeBytes, 0, 4);

                if (typeBytes[0] == 'I' && typeBytes[1] == 'E' && typeBytes[2] == 'N' && typeBytes[3] == 'D')
                {
                    stream.Seek(chunkLength + 4, SeekOrigin.Current);
                    return stream.Position - originalPos;
                }

                if (stream.Length - stream.Position < chunkLength + 4) break;
                stream.Seek(chunkLength + 4, SeekOrigin.Current);
            }
            return 0;
        }
        finally { stream.Seek(originalPos, SeekOrigin.Begin); }
    }
}
