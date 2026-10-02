using Game.Messaging;

namespace Game.ElectricElements;

public class SignElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : ElectricElement(subsystemElectricity, cellFace)
{
    private bool _isMessageAllowed;

    private double? _lastMessageTime;

    public override void OnAdded()
    {
        _isMessageAllowed = CalculateHighInputsCount() == 0;
    }

    public override bool Simulate()
    {
        var flag = CalculateHighInputsCount() > 0;
        if (flag && _isMessageAllowed && (!_lastMessageTime.HasValue ||
                                          SubsystemElectricity.SubsystemTime.GameTime - _lastMessageTime.Value > 0.5))
        {
            _isMessageAllowed = false;
            _lastMessageTime = SubsystemElectricity.SubsystemTime.GameTime;
            var signData = SubsystemElectricity.Project.FindSubsystem<SubsystemSignBlockBehavior>(true)!
                .GetSignData(new Point3(CellFaces[0].X, CellFaces[0].Y, CellFaces[0].Z));
            if (signData != null)
            {
                var message = GameMessage.Sign(CreateMessageSegments(signData));
                SubsystemElectricity.Project.FindSubsystem<SubsystemGameWidgets>(true)!
                    .Messages.Publish(message);
            }
        }

        if (!flag)
        {
            _isMessageAllowed = true;
        }

        return false;
    }

    private static IReadOnlyList<MessageSegment> CreateMessageSegments(SignData signData)
    {
        var firstLine = Array.FindIndex(signData.Lines, line => !string.IsNullOrEmpty(line));
        var lastLine = Array.FindLastIndex(signData.Lines, line => !string.IsNullOrEmpty(line));
        if (firstLine < 0)
        {
            return [];
        }

        var segments = new List<MessageSegment>();
        for (var index = firstLine; index <= lastLine; index++)
        {
            var line = signData.Lines[index];
            var joinsNextLine = line.EndsWith('\\');
            if (joinsNextLine)
            {
                line = line[..^1];
            }

            if (line.Length > 0)
            {
                segments.Add(new MessageSegment(
                    line,
                    Color: NormalizeMessageColor(signData.Colors[index])));
            }

            if (!joinsNextLine && index < lastLine)
            {
                segments.Add(new MessageSegment("\n"));
            }
        }

        return segments;
    }

    private static Color NormalizeMessageColor(Color color)
    {
        if (color == Color.Black)
        {
            return Color.White;
        }

        return color * (255f / MathUtils.Max(color.R, color.G, color.B));
    }
}
