using Game.Blocks.Presentation;

namespace Game.Modding.Blocks;

public static class BuiltInBlockPresentationRegistry
{
    public static IReadOnlyList<BlockPresentationRegistration> Registrations { get; } =
        Array.AsReadOnly<BlockPresentationRegistration>(
            [
                new(
                    ResourceId.Parse("game:air"),
                    static (block, catalog) => new AirBlockPresentation((AirBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:bedrock"),
                    static (block, catalog) => new BedrockBlockPresentation((BedrockBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:dirt"),
                    static (block, catalog) => new DirtBlockPresentation((DirtBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:granite"),
                    static (block, catalog) => new GraniteBlockPresentation((GraniteBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:sandstone"),
                    static (block, catalog) => new SandstoneBlockPresentation((SandstoneBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:cobblestone"),
                    static (block, catalog) => new CobblestoneBlockPresentation((CobblestoneBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:gravel"),
                    static (block, catalog) => new GravelBlockPresentation((GravelBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:sand"),
                    static (block, catalog) => new SandBlockPresentation((SandBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:grass"),
                    static (block, catalog) => new GrassBlockPresentation((GrassBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:oak_wood"),
                    static (block, catalog) => new OakWoodBlockPresentation((OakWoodBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:birch_wood"),
                    static (block, catalog) => new BirchWoodBlockPresentation((BirchWoodBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:spruce_wood"),
                    static (block, catalog) => new SpruceWoodBlockPresentation((SpruceWoodBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:oak_leaves"),
                    static (block, catalog) => new OakLeavesBlockPresentation((OakLeavesBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:birch_leaves"),
                    static (block, catalog) => new BirchLeavesBlockPresentation((BirchLeavesBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:spruce_leaves"),
                    static (block, catalog) => new SpruceLeavesBlockPresentation((SpruceLeavesBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:glass"),
                    static (block, catalog) => new GlassBlockPresentation((GlassBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:coal_ore"),
                    static (block, catalog) => new CoalOreBlockPresentation((CoalOreBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wicker_lamp"),
                    static (block, catalog) => new WickerLampBlockPresentation((WickerLampBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:water"),
                    static (block, catalog) => new WaterBlockPresentation((WaterBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:tall_grass"),
                    static (block, catalog) => new TallGrassBlockPresentation((TallGrassBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:red_flower"),
                    static (block, catalog) => new RedFlowerBlockPresentation((RedFlowerBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:planks"),
                    static (block, catalog) => new PlanksBlockPresentation((PlanksBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:coal_chunk"),
                    static (block, catalog) => new CoalChunkBlockPresentation((CoalChunkBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:stick"),
                    static (block, catalog) => new StickBlockPresentation((StickBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:purple_flower"),
                    static (block, catalog) => new PurpleFlowerBlockPresentation((PurpleFlowerBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:white_flower"),
                    static (block, catalog) => new WhiteFlowerBlockPresentation((WhiteFlowerBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:stone_brick"),
                    static (block, catalog) => new StoneBrickBlockPresentation((StoneBrickBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:crafting_table"),
                    static (block, catalog) => new CraftingTableBlockPresentation((CraftingTableBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:dry_bush"),
                    static (block, catalog) => new DryBushBlockPresentation((DryBushBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:stone_axe"),
                    static (block, catalog) => new StoneAxeBlockPresentation((StoneAxeBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:torch"),
                    static (block, catalog) => new TorchBlockPresentation((TorchBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_shovel"),
                    static (block, catalog) => new IronShovelBlockPresentation((IronShovelBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_axe"),
                    static (block, catalog) => new IronAxeBlockPresentation((IronAxeBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_pickaxe"),
                    static (block, catalog) => new IronPickaxeBlockPresentation((IronPickaxeBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_ore"),
                    static (block, catalog) => new IronOreBlockPresentation((IronOreBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_ingot"),
                    static (block, catalog) => new IronIngotBlockPresentation((IronIngotBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:copper_ore"),
                    static (block, catalog) => new CopperOreBlockPresentation((CopperOreBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:copper_ingot"),
                    static (block, catalog) => new CopperIngotBlockPresentation((CopperIngotBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:malachite_chunk"),
                    static (block, catalog) => new MalachiteChunkBlockPresentation((MalachiteChunkBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:framed_glass"),
                    static (block, catalog) => new FramedGlassBlockPresentation((FramedGlassBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:chest"),
                    static (block, catalog) => new ChestBlockPresentation((ChestBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron"),
                    static (block, catalog) => new IronBlockPresentation((IronBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:copper"),
                    static (block, catalog) => new CopperBlockPresentation((CopperBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:cobblestone_stairs"),
                    static (block, catalog) =>
                        new CobblestoneStairsBlockPresentation((CobblestoneStairsBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wooden_stairs"),
                    static (block, catalog) => new WoodenStairsBlockPresentation((WoodenStairsBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:stone_brick_stairs"),
                    static (block, catalog) =>
                        new StoneBrickStairsBlockPresentation((StoneBrickStairsBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:sandstone_stairs"),
                    static (block, catalog) =>
                        new SandstoneStairsBlockPresentation((SandstoneStairsBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:sandstone_slab"),
                    static (block, catalog) => new SandstoneSlabBlockPresentation((SandstoneSlabBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:cobblestone_slab"),
                    static (block, catalog) =>
                        new CobblestoneSlabBlockPresentation((CobblestoneSlabBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:stone_brick_slab"),
                    static (block, catalog) => new StoneBrickSlabBlockPresentation((StoneBrickSlabBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wooden_slab"),
                    static (block, catalog) => new WoodenSlabBlockPresentation((WoodenSlabBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wooden_door"),
                    static (block, catalog) => new WoodenDoorBlockPresentation((WoodenDoorBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_door"),
                    static (block, catalog) => new IronDoorBlockPresentation((IronDoorBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:cell_door"),
                    static (block, catalog) => new CellDoorBlockPresentation((CellDoorBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wooden_ladder"),
                    static (block, catalog) => new WoodenLadderBlockPresentation((WoodenLadderBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:window"),
                    static (block, catalog) => new WindowBlockPresentation((WindowBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:snow"),
                    static (block, catalog) => new SnowBlockPresentation((SnowBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:ice"),
                    static (block, catalog) => new IceBlockPresentation((IceBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:christmas_tree"),
                    static (block, catalog) => new ChristmasTreeBlockPresentation((ChristmasTreeBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:furnace"),
                    static (block, catalog) => new FurnaceBlockPresentation((FurnaceBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:lit_furnace"),
                    static (block, catalog) => new LitFurnaceBlockPresentation((LitFurnaceBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:limestone"),
                    static (block, catalog) => new LimestoneBlockPresentation((LimestoneBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:basalt"),
                    static (block, catalog) => new BasaltBlockPresentation((BasaltBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:marble"),
                    static (block, catalog) => new MarbleBlockPresentation((MarbleBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:marble_stairs"),
                    static (block, catalog) => new MarbleStairsBlockPresentation((MarbleStairsBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:marble_slab"),
                    static (block, catalog) => new MarbleSlabBlockPresentation((MarbleSlabBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:malachite"),
                    static (block, catalog) => new MalachiteBlockPresentation((MalachiteBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:clay"),
                    static (block, catalog) => new ClayBlockPresentation((ClayBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:bricks"),
                    static (block, catalog) => new BricksBlockPresentation((BricksBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:brick"),
                    static (block, catalog) => new BrickBlockPresentation((BrickBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:brick_slab"),
                    static (block, catalog) => new BrickSlabBlockPresentation((BrickSlabBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:brick_stairs"),
                    static (block, catalog) => new BrickStairsBlockPresentation((BrickStairsBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:raw_bird"),
                    static (block, catalog) => new RawBirdBlockPresentation((RawBirdBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:cooked_bird"),
                    static (block, catalog) => new CookedBirdBlockPresentation((CookedBirdBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:stone_chunk"),
                    static (block, catalog) => new StoneChunkBlockPresentation((StoneChunkBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wooden_spear"),
                    static (block, catalog) => new WoodenSpearBlockPresentation((WoodenSpearBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:stone_spear"),
                    static (block, catalog) => new StoneSpearBlockPresentation((StoneSpearBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_spear"),
                    static (block, catalog) => new IronSpearBlockPresentation((IronSpearBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wooden_trapdoor"),
                    static (block, catalog) => new WoodenTrapdoorBlockPresentation((WoodenTrapdoorBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:cell_trapdoor"),
                    static (block, catalog) => new CellTrapdoorBlockPresentation((CellTrapdoorBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:snowball"),
                    static (block, catalog) => new SnowballBlockPresentation((SnowballBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:spiked_plank"),
                    static (block, catalog) => new SpikedPlankBlockPresentation((SpikedPlankBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:grass_trap"),
                    static (block, catalog) => new GrassTrapBlockPresentation((GrassTrapBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:raw_meat"),
                    static (block, catalog) => new RawMeatBlockPresentation((RawMeatBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:cooked_meat"),
                    static (block, catalog) => new CookedMeatBlockPresentation((CookedMeatBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:empty_bucket"),
                    static (block, catalog) => new EmptyBucketBlockPresentation((EmptyBucketBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:water_bucket"),
                    static (block, catalog) => new WaterBucketBlockPresentation((WaterBucketBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:magma"),
                    static (block, catalog) => new MagmaBlockPresentation((MagmaBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:magma_bucket"),
                    static (block, catalog) => new MagmaBucketBlockPresentation((MagmaBucketBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wooden_fence"),
                    static (block, catalog) => new WoodenFenceBlockPresentation((WoodenFenceBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:basalt_slab"),
                    static (block, catalog) => new BasaltSlabBlockPresentation((BasaltSlabBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:basalt_stairs"),
                    static (block, catalog) => new BasaltStairsBlockPresentation((BasaltStairsBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wooden_posted_sign"),
                    static (block, catalog) =>
                        new WoodenPostedSignBlockPresentation((WoodenPostedSignBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wooden_attached_sign"),
                    static (block, catalog) =>
                        new WoodenAttachedSignBlockPresentation((WoodenAttachedSignBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:large_dry_bush"),
                    static (block, catalog) => new LargeDryBushBlockPresentation((LargeDryBushBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:saltpeter_ore"),
                    static (block, catalog) => new SaltpeterOreBlockPresentation((SaltpeterOreBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:sulphur_ore"),
                    static (block, catalog) => new SulphurOreBlockPresentation((SulphurOreBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:saltpeter_chunk"),
                    static (block, catalog) => new SaltpeterChunkBlockPresentation((SaltpeterChunkBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:sulphur_chunk"),
                    static (block, catalog) => new SulphurChunkBlockPresentation((SulphurChunkBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:fire"),
                    static (block, catalog) => new FireBlockPresentation((FireBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:small_gunpowder_keg"),
                    static (block, catalog) =>
                        new SmallGunpowderKegBlockPresentation((SmallGunpowderKegBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:medium_gunpowder_keg"),
                    static (block, catalog) =>
                        new MediumGunpowderKegBlockPresentation((MediumGunpowderKegBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:large_gunpowder_keg"),
                    static (block, catalog) =>
                        new LargeGunpowderKegBlockPresentation((LargeGunpowderKegBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:match"),
                    static (block, catalog) => new MatchBlockPresentation((MatchBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:gunpowder"),
                    static (block, catalog) => new GunpowderBlockPresentation((GunpowderBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:milk_bucket"),
                    static (block, catalog) => new MilkBucketBlockPresentation((MilkBucketBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:diamond_chunk"),
                    static (block, catalog) => new DiamondChunkBlockPresentation((DiamondChunkBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:diamond_ore"),
                    static (block, catalog) => new DiamondOreBlockPresentation((DiamondOreBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:diamond_shovel"),
                    static (block, catalog) => new DiamondShovelBlockPresentation((DiamondShovelBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:diamond_axe"),
                    static (block, catalog) => new DiamondAxeBlockPresentation((DiamondAxeBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:diamond_pickaxe"),
                    static (block, catalog) => new DiamondPickaxeBlockPresentation((DiamondPickaxeBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:diamond_spear"),
                    static (block, catalog) => new DiamondSpearBlockPresentation((DiamondSpearBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:compass"),
                    static (block, catalog) => new CompassBlockPresentation((CompassBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:egg"),
                    static (block, catalog) => new EggBlockPresentation((EggBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:sapling"),
                    static (block, catalog) => new SaplingBlockPresentation((SaplingBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:thermometer"),
                    static (block, catalog) => new ThermometerBlockPresentation((ThermometerBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:hygrometer"),
                    static (block, catalog) => new HygrometerBlockPresentation((HygrometerBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wooden_club"),
                    static (block, catalog) => new WoodenClubBlockPresentation((WoodenClubBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:stone_club"),
                    static (block, catalog) => new StoneClubBlockPresentation((StoneClubBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_machete"),
                    static (block, catalog) => new IronMacheteBlockPresentation((IronMacheteBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:diamond_machete"),
                    static (block, catalog) => new DiamondMacheteBlockPresentation((DiamondMacheteBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:diamond"),
                    static (block, catalog) => new DiamondBlockPresentation((DiamondBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:cactus"),
                    static (block, catalog) => new CactusBlockPresentation((CactusBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:paint_stripper_bucket"),
                    static (block, catalog) =>
                        new PaintStripperBucketBlockPresentation((PaintStripperBucketBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:paint_bucket"),
                    static (block, catalog) => new PaintBucketBlockPresentation((PaintBucketBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:pigment"),
                    static (block, catalog) => new PigmentBlockPresentation((PigmentBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:pumpkin"),
                    static (block, catalog) => new PumpkinBlockPresentation((PumpkinBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:jack_o_lantern"),
                    static (block, catalog) => new JackOLanternBlockPresentation((JackOLanternBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wire"),
                    static (block, catalog) => new WireBlockPresentation((WireBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:nand_gate"),
                    static (block, catalog) => new NandGateBlockPresentation((NandGateBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:nor_gate"),
                    static (block, catalog) => new NorGateBlockPresentation((NorGateBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:stone_slab"),
                    static (block, catalog) => new StoneSlabBlockPresentation((StoneSlabBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:and_gate"),
                    static (block, catalog) => new AndGateBlockPresentation((AndGateBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:battery"),
                    static (block, catalog) => new BatteryBlockPresentation((BatteryBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:lightbulb"),
                    static (block, catalog) => new LightbulbBlockPresentation((LightbulbBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:not_gate"),
                    static (block, catalog) => new NotGateBlockPresentation((NotGateBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:switch"),
                    static (block, catalog) => new SwitchBlockPresentation((SwitchBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:button"),
                    static (block, catalog) => new ButtonBlockPresentation((ButtonBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:or_gate"),
                    static (block, catalog) => new OrGateBlockPresentation((OrGateBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:pressure_plate"),
                    static (block, catalog) => new PressurePlateBlockPresentation((PressurePlateBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:delay_gate"),
                    static (block, catalog) => new DelayGateBlockPresentation((DelayGateBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:sr_latch"),
                    static (block, catalog) => new SRLatchBlockPresentation((SRLatchBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:detonator"),
                    static (block, catalog) => new DetonatorBlockPresentation((DetonatorBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:germanium_ore"),
                    static (block, catalog) => new GermaniumOreBlockPresentation((GermaniumOreBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:germanium_chunk"),
                    static (block, catalog) => new GermaniumChunkBlockPresentation((GermaniumChunkBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:coal"),
                    static (block, catalog) => new CoalBlockPresentation((CoalBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:photodiode"),
                    static (block, catalog) => new PhotodiodeBlockPresentation((PhotodiodeBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:led"),
                    static (block, catalog) => new LedBlockPresentation((LedBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wire_through_planks"),
                    static (block, catalog) =>
                        new WireThroughPlanksBlockPresentation((WireThroughPlanksBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wire_through_stone"),
                    static (block, catalog) =>
                        new WireThroughStoneBlockPresentation((WireThroughStoneBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wire_through_semiconductor"),
                    static (block, catalog) => new WireThroughSemiconductorBlockPresentation(
                        (WireThroughSemiconductorBlock)block,
                        catalog
                    )
                ),
                new(
                    ResourceId.Parse("game:xor_gate"),
                    static (block, catalog) => new XorGateBlockPresentation((XorGateBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:random_generator"),
                    static (block, catalog) =>
                        new RandomGeneratorBlockPresentation((RandomGeneratorBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:saddle"),
                    static (block, catalog) => new SaddleBlockPresentation((SaddleBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:leather"),
                    static (block, catalog) => new LeatherBlockPresentation((LeatherBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:whistle"),
                    static (block, catalog) => new WhistleBlockPresentation((WhistleBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:raw_fish"),
                    static (block, catalog) => new RawFishBlockPresentation((RawFishBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:cooked_fish"),
                    static (block, catalog) => new CookedFishBlockPresentation((CookedFishBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:basalt_fence"),
                    static (block, catalog) => new BasaltFenceBlockPresentation((BasaltFenceBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:brick_fence"),
                    static (block, catalog) => new BrickFenceBlockPresentation((BrickFenceBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:copper_axe"),
                    static (block, catalog) => new CopperAxeBlockPresentation((CopperAxeBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wooden_fence_gate"),
                    static (block, catalog) =>
                        new WoodenFenceGateBlockPresentation((WoodenFenceGateBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:magnet"),
                    static (block, catalog) => new MagnetBlockPresentation((MagnetBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:soil"),
                    static (block, catalog) => new SoilBlockPresentation((SoilBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wooden_rake"),
                    static (block, catalog) => new WoodenRakeBlockPresentation((WoodenRakeBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_rake"),
                    static (block, catalog) => new IronRakeBlockPresentation((IronRakeBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:diamond_rake"),
                    static (block, catalog) => new DiamondRakeBlockPresentation((DiamondRakeBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:seeds"),
                    static (block, catalog) => new SeedsBlockPresentation((SeedsBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:rye"),
                    static (block, catalog) => new RyeBlockPresentation((RyeBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:flour"),
                    static (block, catalog) => new FlourBlockPresentation((FlourBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:dough"),
                    static (block, catalog) => new DoughBlockPresentation((DoughBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:bread"),
                    static (block, catalog) => new BreadBlockPresentation((BreadBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:boat"),
                    static (block, catalog) => new BoatBlockPresentation((BoatBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:motion_detector"),
                    static (block, catalog) => new MotionDetectorBlockPresentation((MotionDetectorBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:digital_to_analog_converter"),
                    static (block, catalog) => new DigitalToAnalogConverterBlockPresentation(
                        (DigitalToAnalogConverterBlock)block,
                        catalog
                    )
                ),
                new(
                    ResourceId.Parse("game:analog_to_digital_converter"),
                    static (block, catalog) => new AnalogToDigitalConverterBlockPresentation(
                        (AnalogToDigitalConverterBlock)block,
                        catalog
                    )
                ),
                new(
                    ResourceId.Parse("game:four_led"),
                    static (block, catalog) => new FourLedBlockPresentation((FourLedBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:sound_generator"),
                    static (block, catalog) => new SoundGeneratorBlockPresentation((SoundGeneratorBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:counter"),
                    static (block, catalog) => new CounterBlockPresentation((CounterBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:seven_segment_display"),
                    static (block, catalog) =>
                        new SevenSegmentDisplayBlockPresentation((SevenSegmentDisplayBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:memory_bank"),
                    static (block, catalog) => new MemoryBankBlockPresentation((MemoryBankBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:real_time_clock"),
                    static (block, catalog) => new RealTimeClockBlockPresentation((RealTimeClockBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:truth_table_circuit"),
                    static (block, catalog) =>
                        new TruthTableCircuitBlockPresentation((TruthTableCircuitBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:gravestone"),
                    static (block, catalog) => new GravestoneBlockPresentation((GravestoneBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:treasure_generator"),
                    static (block, catalog) =>
                        new TreasureGeneratorBlockPresentation((TreasureGeneratorBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:bow"),
                    static (block, catalog) => new BowBlockPresentation((BowBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:arrow"),
                    static (block, catalog) => new ArrowBlockPresentation((ArrowBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_fence"),
                    static (block, catalog) => new IronFenceBlockPresentation((IronFenceBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_fence_gate"),
                    static (block, catalog) => new IronFenceGateBlockPresentation((IronFenceGateBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:rod"),
                    static (block, catalog) => new RodBlockPresentation((RodBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:feather"),
                    static (block, catalog) => new FeatherBlockPresentation((FeatherBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:ivy"),
                    static (block, catalog) => new IvyBlockPresentation((IvyBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:string"),
                    static (block, catalog) => new StringBlockPresentation((StringBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:target"),
                    static (block, catalog) => new TargetBlockPresentation((TargetBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:crossbow"),
                    static (block, catalog) => new CrossbowBlockPresentation((CrossbowBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:bomb"),
                    static (block, catalog) => new BombBlockPresentation((BombBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:stone_fence"),
                    static (block, catalog) => new StoneFenceBlockPresentation((StoneFenceBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:clothing"),
                    static (block, catalog) => new ClothingBlockPresentation((ClothingBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:cotton"),
                    static (block, catalog) => new CottonBlockPresentation((CottonBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:cotton_wad"),
                    static (block, catalog) => new CottonWadBlockPresentation((CottonWadBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:canvas"),
                    static (block, catalog) => new CanvasBlockPresentation((CanvasBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:fur"),
                    static (block, catalog) => new FurBlockPresentation((FurBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:carpet"),
                    static (block, catalog) => new CarpetBlockPresentation((CarpetBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:campfire"),
                    static (block, catalog) => new CampfireBlockPresentation((CampfireBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_posted_sign"),
                    static (block, catalog) => new IronPostedSignBlockPresentation((IronPostedSignBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_attached_sign"),
                    static (block, catalog) =>
                        new IronAttachedSignBlockPresentation((IronAttachedSignBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:musket"),
                    static (block, catalog) => new MusketBlockPresentation((MusketBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_ladder"),
                    static (block, catalog) => new IronLadderBlockPresentation((IronLadderBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:bullet"),
                    static (block, catalog) => new BulletBlockPresentation((BulletBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:fireworks"),
                    static (block, catalog) => new FireworksBlockPresentation((FireworksBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:dispenser"),
                    static (block, catalog) => new DispenserBlockPresentation((DispenserBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:stone_stairs"),
                    static (block, catalog) => new StoneStairsBlockPresentation((StoneStairsBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:copper_pickaxe"),
                    static (block, catalog) => new CopperPickaxeBlockPresentation((CopperPickaxeBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:copper_rake"),
                    static (block, catalog) => new CopperRakeBlockPresentation((CopperRakeBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:copper_spear"),
                    static (block, catalog) => new CopperSpearBlockPresentation((CopperSpearBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:copper_machete"),
                    static (block, catalog) => new CopperMacheteBlockPresentation((CopperMacheteBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:copper_shovel"),
                    static (block, catalog) => new CopperShovelBlockPresentation((CopperShovelBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wire_through_bricks"),
                    static (block, catalog) =>
                        new WireThroughBricksBlockPresentation((WireThroughBricksBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:adjustable_delay_gate"),
                    static (block, catalog) =>
                        new AdjustableDelayGateBlockPresentation((AdjustableDelayGateBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:tall_spruce_leaves"),
                    static (block, catalog) =>
                        new TallSpruceLeavesBlockPresentation((TallSpruceLeavesBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:sea_urchin"),
                    static (block, catalog) => new SeaUrchinBlockPresentation((SeaUrchinBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:furniture"),
                    static (block, catalog) => new FurnitureBlockPresentation((FurnitureBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:incendiary_bomb"),
                    static (block, catalog) => new IncendiaryBombBlockPresentation((IncendiaryBombBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:starfish"),
                    static (block, catalog) => new StarfishBlockPresentation((StarfishBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_hammer"),
                    static (block, catalog) => new IronHammerBlockPresentation((IronHammerBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:semiconductor"),
                    static (block, catalog) => new SemiconductorBlockPresentation((SemiconductorBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:kelp"),
                    static (block, catalog) => new KelpBlockPresentation((KelpBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:seagrass"),
                    static (block, catalog) => new SeagrassBlockPresentation((SeagrassBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:small_incendiary_keg"),
                    static (block, catalog) =>
                        new SmallIncendiaryKegBlockPresentation((SmallIncendiaryKegBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:medium_incendiary_keg"),
                    static (block, catalog) =>
                        new MediumIncendiaryKegBlockPresentation((MediumIncendiaryKegBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:large_incendiary_keg"),
                    static (block, catalog) =>
                        new LargeIncendiaryKegBlockPresentation((LargeIncendiaryKegBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:piston"),
                    static (block, catalog) => new PistonBlockPresentation((PistonBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:piston_head"),
                    static (block, catalog) => new PistonHeadBlockPresentation((PistonHeadBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:rotten_bird"),
                    static (block, catalog) => new RottenBirdBlockPresentation((RottenBirdBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:rotten_meat"),
                    static (block, catalog) => new RottenMeatBlockPresentation((RottenMeatBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:rotten_fish"),
                    static (block, catalog) => new RottenFishBlockPresentation((RottenFishBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:rotten_bread"),
                    static (block, catalog) => new RottenBreadBlockPresentation((RottenBreadBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:wire_through_cobblestone"),
                    static (block, catalog) => new WireThroughCobblestoneBlockPresentation(
                        (WireThroughCobblestoneBlock)block,
                        catalog
                    )
                ),
                new(
                    ResourceId.Parse("game:rotten_pumpkin"),
                    static (block, catalog) => new RottenPumpkinBlockPresentation((RottenPumpkinBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:rotten_milk_bucket"),
                    static (block, catalog) =>
                        new RottenMilkBucketBlockPresentation((RottenMilkBucketBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:rotten_egg"),
                    static (block, catalog) => new RottenEggBlockPresentation((RottenEggBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:rotten_dough"),
                    static (block, catalog) => new RottenDoughBlockPresentation((RottenDoughBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:experience"),
                    static (block, catalog) => new ExperienceBlockPresentation((ExperienceBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:iron_ore_chunk"),
                    static (block, catalog) => new IronOreChunkBlockPresentation((IronOreChunkBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:germanium_ore_chunk"),
                    static (block, catalog) =>
                        new GermaniumOreChunkBlockPresentation((GermaniumOreChunkBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:pumpkin_soup_bucket"),
                    static (block, catalog) =>
                        new PumpkinSoupBucketBlockPresentation((PumpkinSoupBucketBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:rotten_pumpkin_soup_bucket"),
                    static (block, catalog) => new RottenPumpkinSoupBucketBlockPresentation(
                        (RottenPumpkinSoupBucketBlock)block,
                        catalog
                    )
                ),
                new(
                    ResourceId.Parse("game:one_led"),
                    static (block, catalog) => new OneLedBlockPresentation((OneLedBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:multicolored_led"),
                    static (block, catalog) =>
                        new MulticoloredLedBlockPresentation((MulticoloredLedBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:mimosa_wood"),
                    static (block, catalog) => new MimosaWoodBlockPresentation((MimosaWoodBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:mimosa_leaves"),
                    static (block, catalog) => new MimosaLeavesBlockPresentation((MimosaLeavesBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:shadow"),
                    static (block, catalog) => new ShadowBlockPresentation((ShadowBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:cairn"),
                    static (block, catalog) => new CairnBlockPresentation((CairnBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:dirt_slab"),
                    static (block, catalog) => new DirtSlabBlockPresentation((DirtSlabBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:dirt_stairs"),
                    static (block, catalog) => new DirtStairsBlockPresentation((DirtStairsBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:fallen_leaves"),
                    static (block, catalog) => new FallenLeavesBlockPresentation((FallenLeavesBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:poplar_wood"),
                    static (block, catalog) => new PoplarWoodBlockPresentation((PoplarWoodBlock)block, catalog)
                ),
                new(
                    ResourceId.Parse("game:poplar_leaves"),
                    static (block, catalog) => new PoplarLeavesBlockPresentation((PoplarLeavesBlock)block, catalog)
                ),
            ]
        );
}
