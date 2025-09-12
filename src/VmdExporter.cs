using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;

public static class BoneMapper
{
    private static readonly Dictionary<string, string> KkToMmdMap = new Dictionary<string, string>
    {
        { "", "全ての親" },

        // --- Center / Hips ---
        { "cf_j_hips", "センター" },
        { "cf_t_hips", "センター" },

        // --- Body ---
        { "cf_j_spine01", "上半身" },
        { "cf_j_spine02", "上半身2" },

        // --- Head ---
        { "cf_j_neck", "首" },
        { "cf_j_head", "頭" },

        // --- Left Arm ---
        { "cf_d_shoulder_L", "左肩" },
        { "cf_j_shoulder_L", "左肩" },
        { "cf_j_arm00_L", "左腕" },
        { "cf_j_forearm01_L", "左ひじ" },
        { "cf_t_elbo_L", "左ひじ" },
        { "cf_j_hand_L", "左手首" },
        { "cf_t_hand_L", "左手首" },

        // --- Right Arm ---
        { "cf_d_shoulder_R", "右肩" },
        { "cf_j_shoulder_R", "右肩" },
        { "cf_j_arm00_R", "右腕" },
        { "cf_j_forearm01_R", "右ひじ" },
        { "cf_t_elbo_R", "右ひじ" },
        { "cf_j_hand_R", "右手首" },
        { "cf_t_hand_R", "右手首" },

        // --- Left Leg ---
        { "cf_j_thigh00_L", "左足" },
        { "cf_j_leg01_L", "左ひざ" },
        { "cf_j_leg03_L", "左足首" },

        // --- Right Leg ---
        { "cf_j_thigh00_R", "右足" },
        { "cf_j_leg01_R", "右ひざ" },
        { "cf_j_leg03_R", "右足首" },

        // --- Fingers (Left) ---
        { "cf_j_thumb01_L", "左親指１" }, { "cf_j_thumb02_L", "左親指２" },
        { "cf_j_index01_L", "左人指１" }, { "cf_j_index02_L", "左人指２" }, { "cf_j_index03_L", "左人指３" },
        { "cf_j_middle01_L", "左中指１" }, { "cf_j_middle02_L", "左中指２" }, { "cf_j_middle03_L", "左中指３" },
        { "cf_j_ring01_L", "左薬指１" }, { "cf_j_ring02_L", "左薬指２" }, { "cf_j_ring03_L", "左薬指３" },
        { "cf_j_little01_L", "左小指１" }, { "cf_j_little02_L", "左小指２" }, { "cf_j_little03_L", "左小指３" },

        // --- Fingers (Right) ---
        { "cf_j_thumb01_R", "右親指１" }, { "cf_j_thumb02_R", "右親指２" },
        { "cf_j_index01_R", "右人指１" }, { "cf_j_index02_R", "右人指２" }, { "cf_j_index03_R", "右人指３" },
        { "cf_j_middle01_R", "右中指１" }, { "cf_j_middle02_R", "右中指２" }, { "cf_j_middle03_R", "右中指３" },
        { "cf_j_ring01_R", "右薬指１" }, { "cf_j_ring02_R", "右薬指２" }, { "cf_j_ring03_R", "右薬指３" },
        { "cf_j_little01_R", "右小指１" }, { "cf_j_little02_R", "右小指２" }, { "cf_j_little03_R", "右小指３" },
    };

    public static bool TryGetMmdBone(string kkBone, [NotNullWhen(true)] out string? mmdBone)
    {
        // 處理 Timeline 導軌物件和 KKPE 直接指定的骨骼路徑
        string cleanKkBone = kkBone.Replace("(work)", "").Trim();
        if (cleanKkBone.Contains('/'))
        {
            cleanKkBone = cleanKkBone.Substring(cleanKkBone.LastIndexOf('/') + 1);
        }

        return KkToMmdMap.TryGetValue(cleanKkBone, out mmdBone);
    }
}

public class VmdMotionFrame
{
    public string BoneName { get; set; }
    public uint FrameNumber { get; set; }
    public Vector3? Position { get; set; }
    public Quaternion? Rotation { get; set; }

    public static readonly byte[] DefaultInterpolation = new byte[64] {
        20,  20,   0,   0, 107, 107, 107, 107,  20,  20,  20,  20, 107, 107, 107, 107,
        20,  20,  20, 107, 107, 107, 107,  20,  20,  20,  20, 107, 107, 107, 107,   0,
        20,  20, 107, 107, 107, 107,  20,  20,  20,  20, 107, 107, 107, 107,   0,   0,
        20, 107, 107, 107, 107,  20,  20,  20,  20, 107, 107, 107, 107,   0,   0,   0,
    };

    public VmdMotionFrame(string boneName, uint frameNumber)
    {
        BoneName = boneName;
        FrameNumber = frameNumber;
    }
}

public class VmdExporter
{
    public List<VmdMotionFrame> MotionFrames { get; } = new List<VmdMotionFrame>();

    // 在 VmdExporter.cs 中
    public void Write(string path)
    {
        // VMD requires Shift_JIS encoding
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var sjis = Encoding.GetEncoding("Shift_JIS");

        using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(fs))
        {
            // --- Header (30 bytes) ---
            byte[] headerBytes = new byte[30];
            byte[] vmdStringBytes = sjis.GetBytes("Vocaloid Motion Data 0002");
            Buffer.BlockCopy(vmdStringBytes, 0, headerBytes, 0, vmdStringBytes.Length);
            writer.Write(headerBytes);

            // --- Model Name (20 bytes) ---
            byte[] modelNameBytes = new byte[20];
            byte[] modelStringBytes = sjis.GetBytes("Koikatsu Export");
            Buffer.BlockCopy(modelStringBytes, 0, modelNameBytes, 0, modelStringBytes.Length);
            writer.Write(modelNameBytes);

            // --- Motion Data ---
            writer.Write(MotionFrames.Count);
            foreach (var frame in MotionFrames)
            {
                // Bone Name (15 bytes)
                byte[] nameBytes = new byte[15];
                byte[] tempBytes = sjis.GetBytes(frame.BoneName);
                Buffer.BlockCopy(tempBytes, 0, nameBytes, 0, Math.Min(tempBytes.Length, 15));
                writer.Write(nameBytes);

                writer.Write(frame.FrameNumber);
                writer.Write(frame.Position!.Value.X);
                writer.Write(frame.Position.Value.Y);
                writer.Write(frame.Position.Value.Z);
                writer.Write(frame.Rotation!.Value.X);
                writer.Write(frame.Rotation.Value.Y);
                writer.Write(frame.Rotation.Value.Z);
                writer.Write(frame.Rotation.Value.W);
                writer.Write(VmdMotionFrame.DefaultInterpolation);
            }

            // --- Other Data Sections (write 0 count) ---
            writer.Write(0); // Morph Count
            writer.Write(0); // Camera Count
            writer.Write(0); // Light Count
            writer.Write(0); // Shadow Count
            writer.Write(0); // IK Enable Count
        }
    }
}
