namespace Game.Modding.Blocks;

public static class BuiltInBlockRegistry
{
    public sealed record Entry(BuiltInBlockIdentities.Entry Identity, Type BlockType, Func<Block> Factory)
    {
        public int Index => Identity.Index;

        public string Path => Identity.Path;
    }

    public static IReadOnlyList<Entry> Entries { get; } = Array.AsReadOnly<Entry>(
        [
            new(BuiltInBlockIdentities.Get("air"), typeof(AirBlock), static () => new AirBlock()),
            new(BuiltInBlockIdentities.Get("bedrock"), typeof(BedrockBlock), static () => new BedrockBlock()),
            new(BuiltInBlockIdentities.Get("dirt"), typeof(DirtBlock), static () => new DirtBlock()),
            new(BuiltInBlockIdentities.Get("granite"), typeof(GraniteBlock), static () => new GraniteBlock()),
            new(BuiltInBlockIdentities.Get("sandstone"), typeof(SandstoneBlock), static () => new SandstoneBlock()),
            new(
                BuiltInBlockIdentities.Get("cobblestone"),
                typeof(CobblestoneBlock),
                static () => new CobblestoneBlock()
            ),
            new(BuiltInBlockIdentities.Get("gravel"), typeof(GravelBlock), static () => new GravelBlock()),
            new(BuiltInBlockIdentities.Get("sand"), typeof(SandBlock), static () => new SandBlock()),
            new(BuiltInBlockIdentities.Get("grass"), typeof(GrassBlock), static () => new GrassBlock()),
            new(BuiltInBlockIdentities.Get("oak_wood"), typeof(OakWoodBlock), static () => new OakWoodBlock()),
            new(BuiltInBlockIdentities.Get("birch_wood"), typeof(BirchWoodBlock), static () => new BirchWoodBlock()),
            new(BuiltInBlockIdentities.Get("spruce_wood"), typeof(SpruceWoodBlock), static () => new SpruceWoodBlock()),
            new(BuiltInBlockIdentities.Get("oak_leaves"), typeof(OakLeavesBlock), static () => new OakLeavesBlock()),
            new(
                BuiltInBlockIdentities.Get("birch_leaves"),
                typeof(BirchLeavesBlock),
                static () => new BirchLeavesBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("spruce_leaves"),
                typeof(SpruceLeavesBlock),
                static () => new SpruceLeavesBlock()
            ),
            new(BuiltInBlockIdentities.Get("glass"), typeof(GlassBlock), static () => new GlassBlock()),
            new(BuiltInBlockIdentities.Get("coal_ore"), typeof(CoalOreBlock), static () => new CoalOreBlock()),
            new(BuiltInBlockIdentities.Get("wicker_lamp"), typeof(WickerLampBlock), static () => new WickerLampBlock()),
            new(BuiltInBlockIdentities.Get("water"), typeof(WaterBlock), static () => new WaterBlock()),
            new(BuiltInBlockIdentities.Get("tall_grass"), typeof(TallGrassBlock), static () => new TallGrassBlock()),
            new(BuiltInBlockIdentities.Get("red_flower"), typeof(RedFlowerBlock), static () => new RedFlowerBlock()),
            new(BuiltInBlockIdentities.Get("planks"), typeof(PlanksBlock), static () => new PlanksBlock()),
            new(BuiltInBlockIdentities.Get("coal_chunk"), typeof(CoalChunkBlock), static () => new CoalChunkBlock()),
            new(BuiltInBlockIdentities.Get("stick"), typeof(StickBlock), static () => new StickBlock()),
            new(
                BuiltInBlockIdentities.Get("purple_flower"),
                typeof(PurpleFlowerBlock),
                static () => new PurpleFlowerBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("white_flower"),
                typeof(WhiteFlowerBlock),
                static () => new WhiteFlowerBlock()
            ),
            new(BuiltInBlockIdentities.Get("stone_brick"), typeof(StoneBrickBlock), static () => new StoneBrickBlock()),
            new(
                BuiltInBlockIdentities.Get("crafting_table"),
                typeof(CraftingTableBlock),
                static () => new CraftingTableBlock()
            ),
            new(BuiltInBlockIdentities.Get("dry_bush"), typeof(DryBushBlock), static () => new DryBushBlock()),
            new(BuiltInBlockIdentities.Get("stone_axe"), typeof(StoneAxeBlock), static () => new StoneAxeBlock()),
            new(BuiltInBlockIdentities.Get("torch"), typeof(TorchBlock), static () => new TorchBlock()),
            new(BuiltInBlockIdentities.Get("iron_shovel"), typeof(IronShovelBlock), static () => new IronShovelBlock()),
            new(BuiltInBlockIdentities.Get("iron_axe"), typeof(IronAxeBlock), static () => new IronAxeBlock()),
            new(
                BuiltInBlockIdentities.Get("iron_pickaxe"),
                typeof(IronPickaxeBlock),
                static () => new IronPickaxeBlock()
            ),
            new(BuiltInBlockIdentities.Get("iron_ore"), typeof(IronOreBlock), static () => new IronOreBlock()),
            new(BuiltInBlockIdentities.Get("iron_ingot"), typeof(IronIngotBlock), static () => new IronIngotBlock()),
            new(BuiltInBlockIdentities.Get("copper_ore"), typeof(CopperOreBlock), static () => new CopperOreBlock()),
            new(
                BuiltInBlockIdentities.Get("copper_ingot"),
                typeof(CopperIngotBlock),
                static () => new CopperIngotBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("malachite_chunk"),
                typeof(MalachiteChunkBlock),
                static () => new MalachiteChunkBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("framed_glass"),
                typeof(FramedGlassBlock),
                static () => new FramedGlassBlock()
            ),
            new(BuiltInBlockIdentities.Get("chest"), typeof(ChestBlock), static () => new ChestBlock()),
            new(BuiltInBlockIdentities.Get("iron"), typeof(IronBlock), static () => new IronBlock()),
            new(BuiltInBlockIdentities.Get("copper"), typeof(CopperBlock), static () => new CopperBlock()),
            new(
                BuiltInBlockIdentities.Get("cobblestone_stairs"),
                typeof(CobblestoneStairsBlock),
                static () => new CobblestoneStairsBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("wooden_stairs"),
                typeof(WoodenStairsBlock),
                static () => new WoodenStairsBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("stone_brick_stairs"),
                typeof(StoneBrickStairsBlock),
                static () => new StoneBrickStairsBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("sandstone_stairs"),
                typeof(SandstoneStairsBlock),
                static () => new SandstoneStairsBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("sandstone_slab"),
                typeof(SandstoneSlabBlock),
                static () => new SandstoneSlabBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("cobblestone_slab"),
                typeof(CobblestoneSlabBlock),
                static () => new CobblestoneSlabBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("stone_brick_slab"),
                typeof(StoneBrickSlabBlock),
                static () => new StoneBrickSlabBlock()
            ),
            new(BuiltInBlockIdentities.Get("wooden_slab"), typeof(WoodenSlabBlock), static () => new WoodenSlabBlock()),
            new(BuiltInBlockIdentities.Get("wooden_door"), typeof(WoodenDoorBlock), static () => new WoodenDoorBlock()),
            new(BuiltInBlockIdentities.Get("iron_door"), typeof(IronDoorBlock), static () => new IronDoorBlock()),
            new(BuiltInBlockIdentities.Get("cell_door"), typeof(CellDoorBlock), static () => new CellDoorBlock()),
            new(
                BuiltInBlockIdentities.Get("wooden_ladder"),
                typeof(WoodenLadderBlock),
                static () => new WoodenLadderBlock()
            ),
            new(BuiltInBlockIdentities.Get("window"), typeof(WindowBlock), static () => new WindowBlock()),
            new(BuiltInBlockIdentities.Get("snow"), typeof(SnowBlock), static () => new SnowBlock()),
            new(BuiltInBlockIdentities.Get("ice"), typeof(IceBlock), static () => new IceBlock()),
            new(
                BuiltInBlockIdentities.Get("christmas_tree"),
                typeof(ChristmasTreeBlock),
                static () => new ChristmasTreeBlock()
            ),
            new(BuiltInBlockIdentities.Get("furnace"), typeof(FurnaceBlock), static () => new FurnaceBlock()),
            new(BuiltInBlockIdentities.Get("lit_furnace"), typeof(LitFurnaceBlock), static () => new LitFurnaceBlock()),
            new(BuiltInBlockIdentities.Get("limestone"), typeof(LimestoneBlock), static () => new LimestoneBlock()),
            new(BuiltInBlockIdentities.Get("basalt"), typeof(BasaltBlock), static () => new BasaltBlock()),
            new(BuiltInBlockIdentities.Get("marble"), typeof(MarbleBlock), static () => new MarbleBlock()),
            new(
                BuiltInBlockIdentities.Get("marble_stairs"),
                typeof(MarbleStairsBlock),
                static () => new MarbleStairsBlock()
            ),
            new(BuiltInBlockIdentities.Get("marble_slab"), typeof(MarbleSlabBlock), static () => new MarbleSlabBlock()),
            new(BuiltInBlockIdentities.Get("malachite"), typeof(MalachiteBlock), static () => new MalachiteBlock()),
            new(BuiltInBlockIdentities.Get("clay"), typeof(ClayBlock), static () => new ClayBlock()),
            new(BuiltInBlockIdentities.Get("bricks"), typeof(BricksBlock), static () => new BricksBlock()),
            new(BuiltInBlockIdentities.Get("brick"), typeof(BrickBlock), static () => new BrickBlock()),
            new(BuiltInBlockIdentities.Get("brick_slab"), typeof(BrickSlabBlock), static () => new BrickSlabBlock()),
            new(
                BuiltInBlockIdentities.Get("brick_stairs"),
                typeof(BrickStairsBlock),
                static () => new BrickStairsBlock()
            ),
            new(BuiltInBlockIdentities.Get("raw_bird"), typeof(RawBirdBlock), static () => new RawBirdBlock()),
            new(BuiltInBlockIdentities.Get("cooked_bird"), typeof(CookedBirdBlock), static () => new CookedBirdBlock()),
            new(BuiltInBlockIdentities.Get("stone_chunk"), typeof(StoneChunkBlock), static () => new StoneChunkBlock()),
            new(
                BuiltInBlockIdentities.Get("wooden_spear"),
                typeof(WoodenSpearBlock),
                static () => new WoodenSpearBlock()
            ),
            new(BuiltInBlockIdentities.Get("stone_spear"), typeof(StoneSpearBlock), static () => new StoneSpearBlock()),
            new(BuiltInBlockIdentities.Get("iron_spear"), typeof(IronSpearBlock), static () => new IronSpearBlock()),
            new(
                BuiltInBlockIdentities.Get("wooden_trapdoor"),
                typeof(WoodenTrapdoorBlock),
                static () => new WoodenTrapdoorBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("cell_trapdoor"),
                typeof(CellTrapdoorBlock),
                static () => new CellTrapdoorBlock()
            ),
            new(BuiltInBlockIdentities.Get("snowball"), typeof(SnowballBlock), static () => new SnowballBlock()),
            new(
                BuiltInBlockIdentities.Get("spiked_plank"),
                typeof(SpikedPlankBlock),
                static () => new SpikedPlankBlock()
            ),
            new(BuiltInBlockIdentities.Get("grass_trap"), typeof(GrassTrapBlock), static () => new GrassTrapBlock()),
            new(BuiltInBlockIdentities.Get("raw_meat"), typeof(RawMeatBlock), static () => new RawMeatBlock()),
            new(BuiltInBlockIdentities.Get("cooked_meat"), typeof(CookedMeatBlock), static () => new CookedMeatBlock()),
            new(
                BuiltInBlockIdentities.Get("empty_bucket"),
                typeof(EmptyBucketBlock),
                static () => new EmptyBucketBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("water_bucket"),
                typeof(WaterBucketBlock),
                static () => new WaterBucketBlock()
            ),
            new(BuiltInBlockIdentities.Get("magma"), typeof(MagmaBlock), static () => new MagmaBlock()),
            new(
                BuiltInBlockIdentities.Get("magma_bucket"),
                typeof(MagmaBucketBlock),
                static () => new MagmaBucketBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("wooden_fence"),
                typeof(WoodenFenceBlock),
                static () => new WoodenFenceBlock()
            ),
            new(BuiltInBlockIdentities.Get("basalt_slab"), typeof(BasaltSlabBlock), static () => new BasaltSlabBlock()),
            new(
                BuiltInBlockIdentities.Get("basalt_stairs"),
                typeof(BasaltStairsBlock),
                static () => new BasaltStairsBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("wooden_posted_sign"),
                typeof(WoodenPostedSignBlock),
                static () => new WoodenPostedSignBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("wooden_attached_sign"),
                typeof(WoodenAttachedSignBlock),
                static () => new WoodenAttachedSignBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("large_dry_bush"),
                typeof(LargeDryBushBlock),
                static () => new LargeDryBushBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("saltpeter_ore"),
                typeof(SaltpeterOreBlock),
                static () => new SaltpeterOreBlock()
            ),
            new(BuiltInBlockIdentities.Get("sulphur_ore"), typeof(SulphurOreBlock), static () => new SulphurOreBlock()),
            new(
                BuiltInBlockIdentities.Get("saltpeter_chunk"),
                typeof(SaltpeterChunkBlock),
                static () => new SaltpeterChunkBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("sulphur_chunk"),
                typeof(SulphurChunkBlock),
                static () => new SulphurChunkBlock()
            ),
            new(BuiltInBlockIdentities.Get("fire"), typeof(FireBlock), static () => new FireBlock()),
            new(
                BuiltInBlockIdentities.Get("small_gunpowder_keg"),
                typeof(SmallGunpowderKegBlock),
                static () => new SmallGunpowderKegBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("medium_gunpowder_keg"),
                typeof(MediumGunpowderKegBlock),
                static () => new MediumGunpowderKegBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("large_gunpowder_keg"),
                typeof(LargeGunpowderKegBlock),
                static () => new LargeGunpowderKegBlock()
            ),
            new(BuiltInBlockIdentities.Get("match"), typeof(MatchBlock), static () => new MatchBlock()),
            new(BuiltInBlockIdentities.Get("gunpowder"), typeof(GunpowderBlock), static () => new GunpowderBlock()),
            new(BuiltInBlockIdentities.Get("milk_bucket"), typeof(MilkBucketBlock), static () => new MilkBucketBlock()),
            new(
                BuiltInBlockIdentities.Get("diamond_chunk"),
                typeof(DiamondChunkBlock),
                static () => new DiamondChunkBlock()
            ),
            new(BuiltInBlockIdentities.Get("diamond_ore"), typeof(DiamondOreBlock), static () => new DiamondOreBlock()),
            new(
                BuiltInBlockIdentities.Get("diamond_shovel"),
                typeof(DiamondShovelBlock),
                static () => new DiamondShovelBlock()
            ),
            new(BuiltInBlockIdentities.Get("diamond_axe"), typeof(DiamondAxeBlock), static () => new DiamondAxeBlock()),
            new(
                BuiltInBlockIdentities.Get("diamond_pickaxe"),
                typeof(DiamondPickaxeBlock),
                static () => new DiamondPickaxeBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("diamond_spear"),
                typeof(DiamondSpearBlock),
                static () => new DiamondSpearBlock()
            ),
            new(BuiltInBlockIdentities.Get("compass"), typeof(CompassBlock), static () => new CompassBlock()),
            new(BuiltInBlockIdentities.Get("egg"), typeof(EggBlock), static () => new EggBlock()),
            new(BuiltInBlockIdentities.Get("sapling"), typeof(SaplingBlock), static () => new SaplingBlock()),
            new(
                BuiltInBlockIdentities.Get("thermometer"),
                typeof(ThermometerBlock),
                static () => new ThermometerBlock()
            ),
            new(BuiltInBlockIdentities.Get("hygrometer"), typeof(HygrometerBlock), static () => new HygrometerBlock()),
            new(BuiltInBlockIdentities.Get("wooden_club"), typeof(WoodenClubBlock), static () => new WoodenClubBlock()),
            new(BuiltInBlockIdentities.Get("stone_club"), typeof(StoneClubBlock), static () => new StoneClubBlock()),
            new(
                BuiltInBlockIdentities.Get("iron_machete"),
                typeof(IronMacheteBlock),
                static () => new IronMacheteBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("diamond_machete"),
                typeof(DiamondMacheteBlock),
                static () => new DiamondMacheteBlock()
            ),
            new(BuiltInBlockIdentities.Get("diamond"), typeof(DiamondBlock), static () => new DiamondBlock()),
            new(BuiltInBlockIdentities.Get("cactus"), typeof(CactusBlock), static () => new CactusBlock()),
            new(
                BuiltInBlockIdentities.Get("paint_stripper_bucket"),
                typeof(PaintStripperBucketBlock),
                static () => new PaintStripperBucketBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("paint_bucket"),
                typeof(PaintBucketBlock),
                static () => new PaintBucketBlock()
            ),
            new(BuiltInBlockIdentities.Get("pigment"), typeof(PigmentBlock), static () => new PigmentBlock()),
            new(BuiltInBlockIdentities.Get("pumpkin"), typeof(PumpkinBlock), static () => new PumpkinBlock()),
            new(
                BuiltInBlockIdentities.Get("jack_o_lantern"),
                typeof(JackOLanternBlock),
                static () => new JackOLanternBlock()
            ),
            new(BuiltInBlockIdentities.Get("wire"), typeof(WireBlock), static () => new WireBlock()),
            new(BuiltInBlockIdentities.Get("nand_gate"), typeof(NandGateBlock), static () => new NandGateBlock()),
            new(BuiltInBlockIdentities.Get("nor_gate"), typeof(NorGateBlock), static () => new NorGateBlock()),
            new(BuiltInBlockIdentities.Get("stone_slab"), typeof(StoneSlabBlock), static () => new StoneSlabBlock()),
            new(BuiltInBlockIdentities.Get("and_gate"), typeof(AndGateBlock), static () => new AndGateBlock()),
            new(BuiltInBlockIdentities.Get("battery"), typeof(BatteryBlock), static () => new BatteryBlock()),
            new(BuiltInBlockIdentities.Get("lightbulb"), typeof(LightbulbBlock), static () => new LightbulbBlock()),
            new(BuiltInBlockIdentities.Get("not_gate"), typeof(NotGateBlock), static () => new NotGateBlock()),
            new(BuiltInBlockIdentities.Get("switch"), typeof(SwitchBlock), static () => new SwitchBlock()),
            new(BuiltInBlockIdentities.Get("button"), typeof(ButtonBlock), static () => new ButtonBlock()),
            new(BuiltInBlockIdentities.Get("or_gate"), typeof(OrGateBlock), static () => new OrGateBlock()),
            new(
                BuiltInBlockIdentities.Get("pressure_plate"),
                typeof(PressurePlateBlock),
                static () => new PressurePlateBlock()
            ),
            new(BuiltInBlockIdentities.Get("delay_gate"), typeof(DelayGateBlock), static () => new DelayGateBlock()),
            new(BuiltInBlockIdentities.Get("sr_latch"), typeof(SRLatchBlock), static () => new SRLatchBlock()),
            new(BuiltInBlockIdentities.Get("detonator"), typeof(DetonatorBlock), static () => new DetonatorBlock()),
            new(
                BuiltInBlockIdentities.Get("germanium_ore"),
                typeof(GermaniumOreBlock),
                static () => new GermaniumOreBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("germanium_chunk"),
                typeof(GermaniumChunkBlock),
                static () => new GermaniumChunkBlock()
            ),
            new(BuiltInBlockIdentities.Get("coal"), typeof(CoalBlock), static () => new CoalBlock()),
            new(BuiltInBlockIdentities.Get("photodiode"), typeof(PhotodiodeBlock), static () => new PhotodiodeBlock()),
            new(BuiltInBlockIdentities.Get("led"), typeof(LedBlock), static () => new LedBlock()),
            new(
                BuiltInBlockIdentities.Get("wire_through_planks"),
                typeof(WireThroughPlanksBlock),
                static () => new WireThroughPlanksBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("wire_through_stone"),
                typeof(WireThroughStoneBlock),
                static () => new WireThroughStoneBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("wire_through_semiconductor"),
                typeof(WireThroughSemiconductorBlock),
                static () => new WireThroughSemiconductorBlock()
            ),
            new(BuiltInBlockIdentities.Get("xor_gate"), typeof(XorGateBlock), static () => new XorGateBlock()),
            new(
                BuiltInBlockIdentities.Get("random_generator"),
                typeof(RandomGeneratorBlock),
                static () => new RandomGeneratorBlock()
            ),
            new(BuiltInBlockIdentities.Get("saddle"), typeof(SaddleBlock), static () => new SaddleBlock()),
            new(BuiltInBlockIdentities.Get("leather"), typeof(LeatherBlock), static () => new LeatherBlock()),
            new(BuiltInBlockIdentities.Get("whistle"), typeof(WhistleBlock), static () => new WhistleBlock()),
            new(BuiltInBlockIdentities.Get("raw_fish"), typeof(RawFishBlock), static () => new RawFishBlock()),
            new(BuiltInBlockIdentities.Get("cooked_fish"), typeof(CookedFishBlock), static () => new CookedFishBlock()),
            new(
                BuiltInBlockIdentities.Get("basalt_fence"),
                typeof(BasaltFenceBlock),
                static () => new BasaltFenceBlock()
            ),
            new(BuiltInBlockIdentities.Get("brick_fence"), typeof(BrickFenceBlock), static () => new BrickFenceBlock()),
            new(BuiltInBlockIdentities.Get("copper_axe"), typeof(CopperAxeBlock), static () => new CopperAxeBlock()),
            new(
                BuiltInBlockIdentities.Get("wooden_fence_gate"),
                typeof(WoodenFenceGateBlock),
                static () => new WoodenFenceGateBlock()
            ),
            new(BuiltInBlockIdentities.Get("magnet"), typeof(MagnetBlock), static () => new MagnetBlock()),
            new(BuiltInBlockIdentities.Get("soil"), typeof(SoilBlock), static () => new SoilBlock()),
            new(BuiltInBlockIdentities.Get("wooden_rake"), typeof(WoodenRakeBlock), static () => new WoodenRakeBlock()),
            new(BuiltInBlockIdentities.Get("iron_rake"), typeof(IronRakeBlock), static () => new IronRakeBlock()),
            new(
                BuiltInBlockIdentities.Get("diamond_rake"),
                typeof(DiamondRakeBlock),
                static () => new DiamondRakeBlock()
            ),
            new(BuiltInBlockIdentities.Get("seeds"), typeof(SeedsBlock), static () => new SeedsBlock()),
            new(BuiltInBlockIdentities.Get("rye"), typeof(RyeBlock), static () => new RyeBlock()),
            new(BuiltInBlockIdentities.Get("flour"), typeof(FlourBlock), static () => new FlourBlock()),
            new(BuiltInBlockIdentities.Get("dough"), typeof(DoughBlock), static () => new DoughBlock()),
            new(BuiltInBlockIdentities.Get("bread"), typeof(BreadBlock), static () => new BreadBlock()),
            new(BuiltInBlockIdentities.Get("boat"), typeof(BoatBlock), static () => new BoatBlock()),
            new(
                BuiltInBlockIdentities.Get("motion_detector"),
                typeof(MotionDetectorBlock),
                static () => new MotionDetectorBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("digital_to_analog_converter"),
                typeof(DigitalToAnalogConverterBlock),
                static () => new DigitalToAnalogConverterBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("analog_to_digital_converter"),
                typeof(AnalogToDigitalConverterBlock),
                static () => new AnalogToDigitalConverterBlock()
            ),
            new(BuiltInBlockIdentities.Get("four_led"), typeof(FourLedBlock), static () => new FourLedBlock()),
            new(
                BuiltInBlockIdentities.Get("sound_generator"),
                typeof(SoundGeneratorBlock),
                static () => new SoundGeneratorBlock()
            ),
            new(BuiltInBlockIdentities.Get("counter"), typeof(CounterBlock), static () => new CounterBlock()),
            new(
                BuiltInBlockIdentities.Get("seven_segment_display"),
                typeof(SevenSegmentDisplayBlock),
                static () => new SevenSegmentDisplayBlock()
            ),
            new(BuiltInBlockIdentities.Get("memory_bank"), typeof(MemoryBankBlock), static () => new MemoryBankBlock()),
            new(
                BuiltInBlockIdentities.Get("real_time_clock"),
                typeof(RealTimeClockBlock),
                static () => new RealTimeClockBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("truth_table_circuit"),
                typeof(TruthTableCircuitBlock),
                static () => new TruthTableCircuitBlock()
            ),
            new(BuiltInBlockIdentities.Get("gravestone"), typeof(GravestoneBlock), static () => new GravestoneBlock()),
            new(
                BuiltInBlockIdentities.Get("treasure_generator"),
                typeof(TreasureGeneratorBlock),
                static () => new TreasureGeneratorBlock()
            ),
            new(BuiltInBlockIdentities.Get("bow"), typeof(BowBlock), static () => new BowBlock()),
            new(BuiltInBlockIdentities.Get("arrow"), typeof(ArrowBlock), static () => new ArrowBlock()),
            new(BuiltInBlockIdentities.Get("iron_fence"), typeof(IronFenceBlock), static () => new IronFenceBlock()),
            new(
                BuiltInBlockIdentities.Get("iron_fence_gate"),
                typeof(IronFenceGateBlock),
                static () => new IronFenceGateBlock()
            ),
            new(BuiltInBlockIdentities.Get("rod"), typeof(RodBlock), static () => new RodBlock()),
            new(BuiltInBlockIdentities.Get("feather"), typeof(FeatherBlock), static () => new FeatherBlock()),
            new(BuiltInBlockIdentities.Get("ivy"), typeof(IvyBlock), static () => new IvyBlock()),
            new(BuiltInBlockIdentities.Get("string"), typeof(StringBlock), static () => new StringBlock()),
            new(BuiltInBlockIdentities.Get("target"), typeof(TargetBlock), static () => new TargetBlock()),
            new(BuiltInBlockIdentities.Get("crossbow"), typeof(CrossbowBlock), static () => new CrossbowBlock()),
            new(BuiltInBlockIdentities.Get("bomb"), typeof(BombBlock), static () => new BombBlock()),
            new(BuiltInBlockIdentities.Get("stone_fence"), typeof(StoneFenceBlock), static () => new StoneFenceBlock()),
            new(BuiltInBlockIdentities.Get("clothing"), typeof(ClothingBlock), static () => new ClothingBlock()),
            new(BuiltInBlockIdentities.Get("cotton"), typeof(CottonBlock), static () => new CottonBlock()),
            new(BuiltInBlockIdentities.Get("cotton_wad"), typeof(CottonWadBlock), static () => new CottonWadBlock()),
            new(BuiltInBlockIdentities.Get("canvas"), typeof(CanvasBlock), static () => new CanvasBlock()),
            new(BuiltInBlockIdentities.Get("fur"), typeof(FurBlock), static () => new FurBlock()),
            new(BuiltInBlockIdentities.Get("carpet"), typeof(CarpetBlock), static () => new CarpetBlock()),
            new(BuiltInBlockIdentities.Get("campfire"), typeof(CampfireBlock), static () => new CampfireBlock()),
            new(
                BuiltInBlockIdentities.Get("iron_posted_sign"),
                typeof(IronPostedSignBlock),
                static () => new IronPostedSignBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("iron_attached_sign"),
                typeof(IronAttachedSignBlock),
                static () => new IronAttachedSignBlock()
            ),
            new(BuiltInBlockIdentities.Get("musket"), typeof(MusketBlock), static () => new MusketBlock()),
            new(BuiltInBlockIdentities.Get("iron_ladder"), typeof(IronLadderBlock), static () => new IronLadderBlock()),
            new(BuiltInBlockIdentities.Get("bullet"), typeof(BulletBlock), static () => new BulletBlock()),
            new(BuiltInBlockIdentities.Get("fireworks"), typeof(FireworksBlock), static () => new FireworksBlock()),
            new(BuiltInBlockIdentities.Get("dispenser"), typeof(DispenserBlock), static () => new DispenserBlock()),
            new(
                BuiltInBlockIdentities.Get("stone_stairs"),
                typeof(StoneStairsBlock),
                static () => new StoneStairsBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("copper_pickaxe"),
                typeof(CopperPickaxeBlock),
                static () => new CopperPickaxeBlock()
            ),
            new(BuiltInBlockIdentities.Get("copper_rake"), typeof(CopperRakeBlock), static () => new CopperRakeBlock()),
            new(
                BuiltInBlockIdentities.Get("copper_spear"),
                typeof(CopperSpearBlock),
                static () => new CopperSpearBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("copper_machete"),
                typeof(CopperMacheteBlock),
                static () => new CopperMacheteBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("copper_shovel"),
                typeof(CopperShovelBlock),
                static () => new CopperShovelBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("wire_through_bricks"),
                typeof(WireThroughBricksBlock),
                static () => new WireThroughBricksBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("adjustable_delay_gate"),
                typeof(AdjustableDelayGateBlock),
                static () => new AdjustableDelayGateBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("tall_spruce_leaves"),
                typeof(TallSpruceLeavesBlock),
                static () => new TallSpruceLeavesBlock()
            ),
            new(BuiltInBlockIdentities.Get("sea_urchin"), typeof(SeaUrchinBlock), static () => new SeaUrchinBlock()),
            new(BuiltInBlockIdentities.Get("furniture"), typeof(FurnitureBlock), static () => new FurnitureBlock()),
            new(
                BuiltInBlockIdentities.Get("incendiary_bomb"),
                typeof(IncendiaryBombBlock),
                static () => new IncendiaryBombBlock()
            ),
            new(BuiltInBlockIdentities.Get("starfish"), typeof(StarfishBlock), static () => new StarfishBlock()),
            new(BuiltInBlockIdentities.Get("iron_hammer"), typeof(IronHammerBlock), static () => new IronHammerBlock()),
            new(
                BuiltInBlockIdentities.Get("semiconductor"),
                typeof(SemiconductorBlock),
                static () => new SemiconductorBlock()
            ),
            new(BuiltInBlockIdentities.Get("kelp"), typeof(KelpBlock), static () => new KelpBlock()),
            new(BuiltInBlockIdentities.Get("seagrass"), typeof(SeagrassBlock), static () => new SeagrassBlock()),
            new(
                BuiltInBlockIdentities.Get("small_incendiary_keg"),
                typeof(SmallIncendiaryKegBlock),
                static () => new SmallIncendiaryKegBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("medium_incendiary_keg"),
                typeof(MediumIncendiaryKegBlock),
                static () => new MediumIncendiaryKegBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("large_incendiary_keg"),
                typeof(LargeIncendiaryKegBlock),
                static () => new LargeIncendiaryKegBlock()
            ),
            new(BuiltInBlockIdentities.Get("piston"), typeof(PistonBlock), static () => new PistonBlock()),
            new(BuiltInBlockIdentities.Get("piston_head"), typeof(PistonHeadBlock), static () => new PistonHeadBlock()),
            new(BuiltInBlockIdentities.Get("rotten_bird"), typeof(RottenBirdBlock), static () => new RottenBirdBlock()),
            new(BuiltInBlockIdentities.Get("rotten_meat"), typeof(RottenMeatBlock), static () => new RottenMeatBlock()),
            new(BuiltInBlockIdentities.Get("rotten_fish"), typeof(RottenFishBlock), static () => new RottenFishBlock()),
            new(
                BuiltInBlockIdentities.Get("rotten_bread"),
                typeof(RottenBreadBlock),
                static () => new RottenBreadBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("wire_through_cobblestone"),
                typeof(WireThroughCobblestoneBlock),
                static () => new WireThroughCobblestoneBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("rotten_pumpkin"),
                typeof(RottenPumpkinBlock),
                static () => new RottenPumpkinBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("rotten_milk_bucket"),
                typeof(RottenMilkBucketBlock),
                static () => new RottenMilkBucketBlock()
            ),
            new(BuiltInBlockIdentities.Get("rotten_egg"), typeof(RottenEggBlock), static () => new RottenEggBlock()),
            new(
                BuiltInBlockIdentities.Get("rotten_dough"),
                typeof(RottenDoughBlock),
                static () => new RottenDoughBlock()
            ),
            new(BuiltInBlockIdentities.Get("experience"), typeof(ExperienceBlock), static () => new ExperienceBlock()),
            new(
                BuiltInBlockIdentities.Get("iron_ore_chunk"),
                typeof(IronOreChunkBlock),
                static () => new IronOreChunkBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("germanium_ore_chunk"),
                typeof(GermaniumOreChunkBlock),
                static () => new GermaniumOreChunkBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("pumpkin_soup_bucket"),
                typeof(PumpkinSoupBucketBlock),
                static () => new PumpkinSoupBucketBlock()
            ),
            new(
                BuiltInBlockIdentities.Get("rotten_pumpkin_soup_bucket"),
                typeof(RottenPumpkinSoupBucketBlock),
                static () => new RottenPumpkinSoupBucketBlock()
            ),
            new(BuiltInBlockIdentities.Get("one_led"), typeof(OneLedBlock), static () => new OneLedBlock()),
            new(
                BuiltInBlockIdentities.Get("multicolored_led"),
                typeof(MulticoloredLedBlock),
                static () => new MulticoloredLedBlock()
            ),
            new(BuiltInBlockIdentities.Get("mimosa_wood"), typeof(MimosaWoodBlock), static () => new MimosaWoodBlock()),
            new(
                BuiltInBlockIdentities.Get("mimosa_leaves"),
                typeof(MimosaLeavesBlock),
                static () => new MimosaLeavesBlock()
            ),
            new(BuiltInBlockIdentities.Get("shadow"), typeof(ShadowBlock), static () => new ShadowBlock()),
            new(BuiltInBlockIdentities.Get("cairn"), typeof(CairnBlock), static () => new CairnBlock()),
            new(BuiltInBlockIdentities.Get("dirt_slab"), typeof(DirtSlabBlock), static () => new DirtSlabBlock()),
            new(BuiltInBlockIdentities.Get("dirt_stairs"), typeof(DirtStairsBlock), static () => new DirtStairsBlock()),
            new(
                BuiltInBlockIdentities.Get("fallen_leaves"),
                typeof(FallenLeavesBlock),
                static () => new FallenLeavesBlock()
            ),
            new(BuiltInBlockIdentities.Get("poplar_wood"), typeof(PoplarWoodBlock), static () => new PoplarWoodBlock()),
            new(
                BuiltInBlockIdentities.Get("poplar_leaves"),
                typeof(PoplarLeavesBlock),
                static () => new PoplarLeavesBlock()
            ),
        ]
    );

    private static readonly IReadOnlyDictionary<Type, int> _indices =
        Entries.ToDictionary(entry => entry.BlockType, entry => entry.Index);

    public static int GetIndex<TBlock>() where TBlock : Block => _indices[typeof(TBlock)];
}
