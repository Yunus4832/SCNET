// Minimal voxel beacon. Millimetres: 1000 mm = one game block.
module Base() {
    translate([-400,-400,0]) cube([800,800,100]);
}
module Glass() {
    translate([-320,-320,100]) cube([640,640,400]);
}
module Core() {
    translate([0,0,100]) intersection() {
        sphere(r=240,$fn=20);
        translate([-240,-240,0]) cube([480,480,240]);
    }
}
Base(); Glass(); Core();
