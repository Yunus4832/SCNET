namespace Game.ElectricElements;

public class SignElectricElement(
    SubsystemElectricity subsystemElectricity,
    CellFace cellFace
) : ElectricElement(subsystemElectricity, cellFace)
{
    private bool _isMessageAllowed = true;

    private double? _lastMessageTime;

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
                var text = string.Join("\n", signData.Lines);
                text = text.Trim('\n');
                text = text.Replace("\\\n", "");
                var color = signData.Colors[0] == Color.Black ? Color.White : signData.Colors[0];
                color *= 255f / MathUtils.Max(color.R, color.G, color.B);
                foreach (var componentPlayer in SubsystemElectricity.Project.FindSubsystem<SubsystemPlayers>(true)!
                             .ComponentPlayers)
                {
                    componentPlayer.ComponentGui.DisplaySmallMessage(text, color, true, true);
                }
            }
        }

        if (!flag)
        {
            _isMessageAllowed = true;
        }

        return false;
    }
}
