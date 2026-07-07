namespace Game.ContentReaders;

public class MtllibStruct
{
    public Dictionary<string, string> TexturePaths = new();

    public static MtllibStruct Load(Stream stream)
    {
        var mtllibStruct = new MtllibStruct();
        using (stream)
        {
            var streamReader = new StreamReader(stream);
            string? tKey = null;
            while (!streamReader.EndOfStream)
            {
                var line = streamReader.ReadLine();
                if (line != null)
                {
                    var spl = line.Split([(char)0x09, (char)0x20], StringSplitOptions.None);
                    switch (spl[0])
                    {
                        case "newmtl":
                        {
                            tKey = spl[1];
                            break;
                        }

                        case "map_Kd":
                        {
                            if (string.IsNullOrEmpty(tKey))
                            {
                                throw new Exception("请先newmtl");
                            }

                            mtllibStruct.TexturePaths.Add(tKey, spl[1]);
                            break;
                        }
                    }
                }
            }
        }

        return mtllibStruct;
    }
}
