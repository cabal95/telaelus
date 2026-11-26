# Item models

One small object per resource type (63 in all), rendered once at startup into the resource icons the UI
shows (`ResourceIcons`, `src/Telaelus/Presentation/Ui/`). Each is a single chunky object about 1 unit
across, resting on y = 0, built to read as a silhouette at icon size. The look follows
`docs/blueprints/art-style.md`.

Every file is required: the game throws if one is missing.

| Resource | File | Item |
|---|---|---|
| Wood | `wood.glb` | Three stacked logs with light end rings |
| Stone | `stone.glb` | A rough gray block |
| Iron Ore | `iron_ore.glb` | A rock lump with rust-red veins |
| Copper Ore | `copper_ore.glb` | A rock lump with green and orange veins |
| Coal | `coal.glb` | An angular black lump |
| Crude Oil | `crude_oil.glb` | A dark barrel with brown bands |
| Sand | `sand.glb` | A low tan heap |
| Water | `water.glb` | A classic teardrop, its tip leaned away from the game camera so the icon shows its profile |
| Iron | `iron.glb` | Two gray ingots, one across the other |
| Rocket Fuel | `rocket_fuel.glb` | A white canister with an orange band and a dark cap |
| Bauxite | `bauxite.glb` | A red-brown rock lump with pale veins |
| Limestone | `limestone.glb` | A rough cream block |
| Quartz | `quartz.glb` | A cluster of four pale six-sided crystals |
| Titanium Ore | `titanium_ore.glb` | A dark rock lump with silver veins |
| Sulfur | `sulfur.glb` | A low yellow heap |
| Rare Earths | `rare_earths.glb` | A pale rock lump with pink and violet veins |
| Uranium Ore | `uranium_ore.glb` | A dark rock lump with glowing green veins |
| Crystals | `crystals.glb` | A cluster of five violet crystals with glowing tips |
| Lithium | `lithium.glb` | A low white heap flecked with pink |
| Copper | `copper.glb` | Two copper ingots, one across the other |
| Steel | `steel.glb` | Two blue-gray ingots, one across the other |
| Glass | `glass.glb` | Three pale blue panes standing in a wooden rack |
| Lumber | `lumber.glb` | A crossed stack of sawn planks |
| Charcoal | `charcoal.glb` | Three charred logs stacked two and one |
| Cement | `cement.glb` | A tan sack with a gray band |
| Concrete | `concrete.glb` | Two cast gray blocks, one on the other |
| Kerosene | `kerosene.glb` | A white canister with a yellow band |
| Plastic | `plastic.glb` | A white sack with a blue band |
| Hydrogen | `hydrogen.glb` | A tall white gas bottle with a red band |
| Liquid Oxygen | `liquid_oxygen.glb` | A tall frosty blue gas bottle with a blue band |
| Aluminum | `aluminum.glb` | Two light silver ingots, one across the other |
| Silicon | `silicon.glb` | A blue-gray grown boule on its side |
| Sulfuric Acid | `sulfuric_acid.glb` | A yellow canister with a black band |
| Rubber | `rubber.glb` | Two gray tires stacked flat |
| Titanium | `titanium.glb` | Two dark blue-gray ingots, one across the other |
| Carbon Fiber | `carbon_fiber.glb` | A gray-blue rolled sheet on its side |
| Enriched Uranium | `enriched_uranium.glb` | Glowing green pellets in a squat lead pot |
| Hull Plate | `hull_plate.glb` | A stack of three steel plates, each turned a little |
| Steel Beam | `steel_beam.glb` | A steel I-beam lying flat |
| Copper Wire | `copper_wire.glb` | A wooden spool wound with copper wire |
| Circuit | `circuit.glb` | A green board with black chips and gold contacts |
| Insulated Cable | `insulated_cable.glb` | A wooden spool wound with red cable |
| Machine Parts | `machine_parts.glb` | A toothed gear with a smaller one on top |
| Silicon Wafer | `silicon_wafer.glb` | A thin violet-gray disc ruled into dies |
| Processor | `processor.glb` | A black chip with a metal lid and gold pins |
| Magnet | `magnet.glb` | A red horseshoe magnet with silver poles |
| Electric Motor | `electric_motor.glb` | A blue finned motor on feet with its shaft out |
| Battery | `battery.glb` | A green cell with a silver base and terminal |
| Solar Cell | `solar_cell.glb` | A framed blue cell ruled into squares |
| Composite Panel | `composite_panel.glb` | Two gray-blue panels stacked |
| Ceramic Tile | `ceramic_tile.glb` | A stack of four white tiles |
| Aluminum Frame | `aluminum_frame.glb` | An open cube of silver struts |
| Armor Plate | `armor_plate.glb` | A thick olive plate with corner bolts |
| Control Module | `control_module.glb` | A dark console with a glowing screen and colored buttons |
| Computer | `computer.glb` | A dark tower with a column of status lights |
| Thruster Assembly | `thruster_assembly.glb` | A chamber over a bell nozzle with a hot throat |
| Fuel Tank | `fuel_tank.glb` | A white capsule tank on its side with orange straps |
| Heat Shield | `heat_shield.glb` | A shallow gray dome |
| Fuel Rod | `fuel_rod.glb` | A bundle of rods with glowing tips, banded together |
| Life Support Unit | `life_support_unit.glb` | A cabinet with a fan grille and a green tank |
| Sensor Array | `sensor_array.glb` | A dish on a mast tipped toward the sky |
| Hull Section | `hull_section.glb` | A curved panel of hull plating with rivets |
| Engine | `engine.glb` | A tank-fed body over a large bell nozzle |

Every model here is generated by `src/asset-factory/tools/item_gen.py` (see
`docs/asset-pipeline.md`). The file name is `item_` and the resource's name in snake case.
