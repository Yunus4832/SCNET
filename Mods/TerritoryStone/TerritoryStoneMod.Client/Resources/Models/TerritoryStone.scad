// 毫米，底面中心为原点。削去碑角使正面纹章在小尺寸下仍然清晰。
module section(z, half, bevel) {
    translate([0, 0, z]) linear_extrude(0.1)
        polygon([[-half+bevel,-half],[half-bevel,-half],[half,-half+bevel],
                 [half,half-bevel],[half-bevel,half],[-half+bevel,half],
                 [-half,half-bevel],[-half,-half+bevel]]);
}

levels = [[0,440,78],[75,440,78],[140,375,62],[175,375,62],
          [190,316,46],[695,288,44],[717,304,48],[757,304,48],
          [772,287,44],[868,278,44],[980,164,38]];
for (i = [0:len(levels)-2]) hull() {
    section(levels[i][0], levels[i][1], levels[i][2]);
    section(levels[i+1][0], levels[i+1][1], levels[i+1][2]);
}
