# Item models

One small object per resource type (63 in all), rendered once at startup into the resource icons the UI
shows (`ResourceIcons`, `src/Telaelus/Presentation/Ui/`). Each is a single chunky object about 1 unit
across, resting on y = 0, built to read as a silhouette at icon size. The look follows
`docs/blueprints/art-style.md`.

Every file is required: the game throws if one is missing.

| Resource | File | Item |
|---|---|---|
| Wood | `item_wood.glb` | Three stacked logs with light end rings |
| Stone | `item_stone.glb` | A rough gray block |
| Iron Ore | `item_iron_ore.glb` | A rock lump with rust-red veins |
| Copper Ore | `item_copper_ore.glb` | A rock lump with green and orange veins |
| Coal | `item_coal.glb` | An angular black lump |
| Crude Oil | `item_crude_oil.glb` | A dark barrel with brown bands |
| Sand | `item_sand.glb` | A low tan heap |
| Water | `item_water.glb` | A classic teardrop, its tip leaned away from the game camera so the icon shows its profile |
| Iron | `item_iron.glb` | Two gray ingots, one across the other |
| Rocket Fuel | `item_rocket_fuel.glb` | A white canister with an orange band and a dark cap |
| Bauxite | `item_bauxite.glb` | A red-brown rock lump with pale veins |
| Limestone | `item_limestone.glb` | A rough cream block |
| Quartz | `item_quartz.glb` | A cluster of four pale six-sided crystals |
| Titanium Ore | `item_titanium_ore.glb` | A dark rock lump with silver veins |
| Sulfur | `item_sulfur.glb` | A low yellow heap |
| Rare Earths | `item_rare_earths.glb` | A pale rock lump with pink and violet veins |
| Uranium Ore | `item_uranium_ore.glb` | A dark rock lump with glowing green veins |
| Crystals | `item_crystals.glb` | A cluster of five violet crystals with glowing tips |
| Lithium | `item_lithium.glb` | A low white heap flecked with pink |
| Copper | `item_copper.glb` | Two copper ingots, one across the other |
| Steel | `item_steel.glb` | Two blue-gray ingots, one across the other |
| Glass | `item_glass.glb` | Three pale blue panes standing in a wooden rack |
| Lumber | `item_lumber.glb` | A crossed stack of sawn planks |
| Charcoal | `item_charcoal.glb` | Three charred logs stacked two and one |
| Cement | `item_cement.glb` | A tan sack with a gray band |
| Concrete | `item_concrete.glb` | Two cast gray blocks, one on the other |
| Kerosene | `item_kerosene.glb` | A white canister with a yellow band |
| Plastic | `item_plastic.glb` | A white sack with a blue band |
| Hydrogen | `item_hydrogen.glb` | A tall white gas bottle with a red band |
| Liquid Oxygen | `item_liquid_oxygen.glb` | A tall frosty blue gas bottle with a blue band |
| Aluminum | `item_aluminum.glb` | Two light silver ingots, one across the other |
| Silicon | `item_silicon.glb` | A blue-gray grown boule on its side |
| Sulfuric Acid | `item_sulfuric_acid.glb` | A yellow canister with a black band |
| Rubber | `item_rubber.glb` | Two gray tires stacked flat |
| Titanium | `item_titanium.glb` | Two dark blue-gray ingots, one across the other |
| Carbon Fiber | `item_carbon_fiber.glb` | A gray-blue rolled sheet on its side |
| Enriched Uranium | `item_enriched_uranium.glb` | Glowing green pellets in a squat lead pot |
| Hull Plate | `item_hull_plate.glb` | A stack of three steel plates, each turned a little |
| Steel Beam | `item_steel_beam.glb` | A steel I-beam lying flat |
| Copper Wire | `item_copper_wire.glb` | A wooden spool wound with copper wire |
| Circuit | `item_circuit.glb` | A green board with black chips and gold contacts |
| Insulated Cable | `item_insulated_cable.glb` | A wooden spool wound with red cable |
| Machine Parts | `item_machine_parts.glb` | A toothed gear with a smaller one on top |
| Silicon Wafer | `item_silicon_wafer.glb` | A thin violet-gray disc ruled into dies |
| Processor | `item_processor.glb` | A black chip with a metal lid and gold pins |
| Magnet | `item_magnet.glb` | A red horseshoe magnet with silver poles |
| Electric Motor | `item_electric_motor.glb` | A blue finned motor on feet with its shaft out |
| Battery | `item_battery.glb` | A green cell with a silver base and terminal |
| Solar Cell | `item_solar_cell.glb` | A framed blue cell ruled into squares |
| Composite Panel | `item_composite_panel.glb` | Two gray-blue panels stacked |
| Ceramic Tile | `item_ceramic_tile.glb` | A stack of four white tiles |
| Aluminum Frame | `item_aluminum_frame.glb` | An open cube of silver struts |
| Armor Plate | `item_armor_plate.glb` | A thick olive plate with corner bolts |
| Control Module | `item_control_module.glb` | A dark console with a glowing screen and colored buttons |
| Computer | `item_computer.glb` | A dark tower with a column of status lights |
| Thruster Assembly | `item_thruster_assembly.glb` | A chamber over a bell nozzle with a hot throat |
| Fuel Tank | `item_fuel_tank.glb` | A white capsule tank on its side with orange straps |
| Heat Shield | `item_heat_shield.glb` | A shallow gray dome |
| Fuel Rod | `item_fuel_rod.glb` | A bundle of rods with glowing tips, banded together |
| Life Support Unit | `item_life_support_unit.glb` | A cabinet with a fan grille and a green tank |
| Sensor Array | `item_sensor_array.glb` | A dish on a mast tipped toward the sky |
| Hull Section | `item_hull_section.glb` | A curved panel of hull plating with rivets |
| Engine | `item_engine.glb` | A tank-fed body over a large bell nozzle |

Every model here is generated by `src/asset-factory/tools/item_gen.py` (see
`docs/asset-pipeline.md`). The file name is `item_` and the resource's name in snake case.
