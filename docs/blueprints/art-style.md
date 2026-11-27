# Art Style Blueprint

## Overview

How every asset in the on-planet game looks and is built: the look, the view it is judged from,
the shared palette, scale, how cell-sized models fit together, variants, where assets come from,
and naming. New assets (roads, pipes, bridges, vehicles, new buildings) follow these rules. How to
generate, preview, and export an asset is in docs/asset-pipeline.md.

## Current state

### Look

- Low-poly and flat-shaded: faceted surfaces, no smoothing, every triangle one color.
- Flat colors from material base colors; no textures. Emission only for glowing materials (lava,
  the furnace mouth). Materials are matte (no metal, no specular).
- Small surface details (grass patches, sand ripples, pebbles, puddles) stay nearly flat. The game
  lights with one sun and no ambient light (an environment with ambient off, linear tone mapping,
  and hard-edged shadows; see presentation.md; the light keeps its angle through the day, warming
  at dawn and dusk, and by night turns to a dimmer blue moonlight), so any steep face turned from the sun reads as
  black; a small steep detail becomes a black speck. The look was set under the Compatibility
  renderer and matched under the Mobile renderer side by side.

### View

- An orthographic camera at yaw 45 and pitch -48 with one shadowed directional light; the tilt
  comes from the camera, not the art. The camera looks toward the north-west, so a model's south
  and east sides face the viewer.
- Assets are judged at the default zoom from that angle: silhouette and color first, detail second.

### Palette

Base colors (sRGB), defined once as `PALETTE` in `src/asset-factory/tools/mesh.py`. Generators
take colors only from this table, as the base color or a shade of it (a multiplier such as 0.94
or 1.05 for facet variation). A new material extends the table.

| Name | Color | Use |
|---|---|---|
| grass | 0.33, 0.49, 0.27 | Grass ground |
| sand | 0.78, 0.70, 0.48 | Sand ground |
| salt_flat | 0.88, 0.86, 0.80 | Salt flat ground (a warm off-white, apart from snow's cool white) |
| salt_crack | 0.70, 0.67, 0.60 | The ridges of a salt flat's crust |
| snow | 0.90, 0.92, 0.95 | Snow ground |
| rock | 0.48, 0.46, 0.44 | Rock ground, pebbles |
| mud | 0.38, 0.32, 0.22 | Mud ground |
| shallow_water | 0.20, 0.38, 0.60 | Shallow water, river water |
| deep_water | 0.10, 0.22, 0.42 | Deep water |
| lava | 0.95, 0.42, 0.10 | Lava (emissive) |
| lava_crust | 0.24, 0.13, 0.10 | Lava crust plates |
| wet_earth | 0.27, 0.23, 0.18 | River channel sides |
| puddle | 0.22, 0.24, 0.24 | Mud puddles |
| stone | 0.50, 0.47, 0.44 | Building stone |
| stone_dark | 0.36, 0.34, 0.33 | Foundations, rock slabs |
| wood | 0.58, 0.41, 0.24 | Timber, planks |
| wood_dark | 0.40, 0.28, 0.17 | Posts, logs |
| metal | 0.58, 0.60, 0.63 | Tanks, saw blade |
| metal_dark | 0.30, 0.31, 0.34 | Pipes, rails, wheels |
| shadow | 0.08, 0.07, 0.07 | Openings (doors, shafts, chimney mouth) |
| furnace_glow | 1.00, 0.55, 0.18 | Furnace mouth (emissive) |
| ore_rock | 0.52, 0.51, 0.50 | Ore host rock |
| ore_rock_dark | 0.40, 0.40, 0.41 | Ore host rock, shaded faces |
| iron_rust | 0.66, 0.31, 0.17 | Iron Ore veins |
| iron_dark | 0.45, 0.20, 0.12 | Iron Ore veins, dark |
| iron_steel | 0.30, 0.33, 0.42 | Iron Ore chunks |
| copper_rock | 0.50, 0.44, 0.38 | Copper Ore host rock |
| copper_green | 0.22, 0.58, 0.45 | Copper Ore veins (malachite) |
| copper_orange | 0.82, 0.46, 0.22 | Copper Ore veins and chunks |
| coal | 0.13, 0.13, 0.14 | Coal lumps |
| coal_sheen | 0.24, 0.24, 0.27 | Coal lump tops |
| boulder | 0.70, 0.68, 0.64 | Stone boulders |
| boulder_dark | 0.56, 0.54, 0.51 | Stone boulder bases |
| oil | 0.05, 0.05, 0.06 | Crude Oil pool, Oil Well parts |
| oil_sheen | 0.20, 0.19, 0.26 | Crude Oil pool rim sheen |
| oil_violet | 0.28, 0.18, 0.36 | Crude Oil pool rim sheen |
| oil_teal | 0.12, 0.30, 0.30 | Crude Oil pool rim sheen |
| oil_earth | 0.30, 0.25, 0.19 | Oil-stained ground around a pool |
| bark | 0.45, 0.32, 0.21 | Tree trunks |
| bark_dark | 0.34, 0.24, 0.16 | Trunk ends, swamp tree roots |
| conifer, conifer_light, conifer_dark | 0.20, 0.42, 0.33 / 0.27, 0.52, 0.40 / 0.16, 0.34, 0.27 | Conifer tiers (Forest) |
| leaf, leaf_light, leaf_dark | 0.33, 0.56, 0.26 / 0.44, 0.66, 0.31 / 0.25, 0.45, 0.21 | Broadleaf crowns (Grassland) |
| swamp_leaf, swamp_leaf_light, swamp_leaf_dark | 0.40, 0.46, 0.21 / 0.49, 0.54, 0.26 / 0.31, 0.37, 0.17 | Swamp tree crowns |
| moss | 0.34, 0.40, 0.19 | Swamp tree hanging fringe |
| log_end | 0.80, 0.66, 0.45 | Wood item: cut log ends |
| coal_facet | 0.36, 0.36, 0.40 | Coal item: the upper facets that catch the light |
| barrel, barrel_lid, barrel_band | 0.24, 0.24, 0.27 / 0.38, 0.38, 0.42 / 0.45, 0.30, 0.18 | Crude Oil item |
| water, water_light | 0.26, 0.52, 0.86 / 0.45, 0.70, 0.95 | Water item |
| ingot, ingot_light | 0.62, 0.64, 0.68 / 0.78, 0.80, 0.84 | Iron item: ingot sides and tops |
| canister, fuel_orange | 0.88, 0.88, 0.86 / 0.95, 0.50, 0.15 | Rocket Fuel item |
| bauxite_rock, bauxite_dark, bauxite_pale | 0.62, 0.38, 0.26 / 0.46, 0.27, 0.19 / 0.84, 0.66, 0.48 | Bauxite item |
| titanium_rock, titanium_vein | 0.30, 0.31, 0.33 / 0.70, 0.72, 0.78 | Titanium Ore item |
| rare_rock, rare_pink, rare_violet | 0.66, 0.60, 0.58 / 0.86, 0.44, 0.62 / 0.58, 0.36, 0.72 | Rare Earths item (pink also flecks Lithium) |
| uranium_rock, uranium_glow | 0.28, 0.29, 0.26 / 0.62, 0.92, 0.22 | Uranium Ore item; the glow also lights Enriched Uranium and Fuel Rods (uranium_glow emissive) |
| quartz, quartz_shade | 0.90, 0.90, 0.94 / 0.72, 0.74, 0.82 | Quartz item |
| crystal, crystal_light | 0.62, 0.36, 0.92 / 0.82, 0.62, 1.00 | Crystals item (crystal_light emissive) |
| limestone, limestone_dark | 0.86, 0.82, 0.70 / 0.72, 0.68, 0.57 | Limestone item |
| sulfur | 0.92, 0.84, 0.20 | Sulfur item |
| lithium | 0.92, 0.88, 0.90 | Lithium item |
| charcoal, charcoal_end | 0.26, 0.24, 0.23 / 0.52, 0.45, 0.38 | Charcoal item |
| copper, copper_light | 0.86, 0.50, 0.28 / 0.96, 0.66, 0.42 | Copper item; copper wire and pipes on parts |
| steel, steel_light | 0.46, 0.52, 0.60 / 0.62, 0.68, 0.76 | Steel item, Hull Plate, Steel Beam |
| aluminum, aluminum_light | 0.80, 0.82, 0.86 / 0.92, 0.94, 0.96 | Aluminum item, Aluminum Frame |
| titanium, titanium_light | 0.42, 0.44, 0.52 / 0.58, 0.60, 0.70 | Titanium item |
| silicon, silicon_light | 0.38, 0.40, 0.50 / 0.56, 0.58, 0.72 | Silicon item |
| lead | 0.36, 0.37, 0.40 | Enriched Uranium pot, Fuel Rod bands |
| plank, plank_dark | 0.80, 0.62, 0.40 / 0.66, 0.50, 0.32 | Lumber item |
| concrete, concrete_dark | 0.66, 0.66, 0.64 / 0.54, 0.54, 0.52 | Concrete item, Cement sack band |
| glass, glass_edge | 0.62, 0.82, 0.90 / 0.80, 0.92, 0.96 | Glass item |
| sack, sack_white | 0.74, 0.70, 0.62 / 0.92, 0.92, 0.90 | Cement and Plastic sacks |
| band_blue, band_red, band_yellow, band_green | 0.24, 0.46, 0.82 / 0.82, 0.24, 0.20 / 0.94, 0.80, 0.18 / 0.40, 0.72, 0.28 | Colored bands and bodies on canisters, cells, and parts |
| frost | 0.78, 0.90, 0.98 | Liquid Oxygen bottle |
| rubber, rubber_light | 0.30, 0.30, 0.32 / 0.48, 0.48, 0.52 | Rubber item (tires) |
| carbon, carbon_weave | 0.30, 0.31, 0.36 / 0.50, 0.52, 0.60 | Carbon Fiber, Composite Panel, Heat Shield |
| board | 0.18, 0.50, 0.30 | Circuit board |
| chip | 0.12, 0.12, 0.13 | Chips on the Circuit and Processor |
| gold | 0.92, 0.74, 0.24 | Contacts and pins |
| wafer | 0.46, 0.44, 0.62 | Silicon Wafer |
| solar, solar_line | 0.16, 0.26, 0.56 / 0.56, 0.62, 0.74 | Solar Cell; the lines also rule the wafer |
| magnet_red | 0.84, 0.20, 0.18 | Magnet item |
| ceramic | 0.90, 0.88, 0.84 | Ceramic Tile item |
| armor | 0.38, 0.42, 0.34 | Armor Plate item |
| panel_dark | 0.24, 0.26, 0.30 | Control Module and Computer cases |
| screen, led | 0.30, 0.78, 0.86 / 0.40, 0.95, 0.50 | Screens and status lights (screen, led emissive) |
| nozzle, nozzle_hot | 0.34, 0.34, 0.38 / 0.90, 0.48, 0.20 | Thruster and Engine bells and their hot throats (nozzle_hot emissive) |
| hull, hull_dark | 0.80, 0.82, 0.84 / 0.58, 0.60, 0.64 | Hull Section, Sensor Array, Life Support Unit |
| suit, skin, hair, boot | 0.90, 0.46, 0.14 / 0.88, 0.68, 0.52 / 0.32, 0.22, 0.14 / 0.24, 0.22, 0.21 | The player character |
| mine_accent | 0.85, 0.55, 0.20 | Mine identity color (orange) |
| lumber_accent | 0.62, 0.40, 0.20 | Lumber Camp identity color (brown) |
| smelter_accent | 0.78, 0.26, 0.20 | Smelter identity color (red) |
| refinery_accent | 0.22, 0.42, 0.78 | Refinery identity color (blue) |
| oil_well_accent | 0.92, 0.74, 0.16 | Oil Well identity color (yellow, with oil black) |
| hq_accent | 0.16, 0.60, 0.56 | HQ identity color (teal) |
| silo_accent | 0.42, 0.66, 0.28 | Silo identity color (green) |
| stockyard_accent | 0.56, 0.40, 0.74 | Stockyard identity color (purple) |
| tank_farm_accent | 0.30, 0.78, 0.92 | Tank Farm identity color (cyan) |
| warehouse_accent | 0.84, 0.36, 0.58 | Warehouse identity color (pink) |
| vault_accent | 0.78, 0.62, 0.22 | Vault identity color (gold) |
| hub_accent | 0.30, 0.95, 0.70 | The Small Hub's and Relay's glowing ring (emissive) |
| water_wheel_accent | 0.25, 0.58, 0.92 | Water Wheel identity color (blue paddles and housing roof) |
| burner_accent | 0.88, 0.36, 0.16 | Burner Generator identity color (orange-red) |
| power_accent | 1.00, 0.86, 0.12 | The powered family's mark: power pole bands, stripes, junction boxes |
| water_accent | 0.40, 0.74, 0.98 | Water Pump and Pumping Station identity color (light blue roof, bands, valve) |
| sand_accent | 0.90, 0.72, 0.42 | Sand Collector and Sand Dredge identity color (tan bands, chute, scoops) |
| glass_accent | 0.66, 0.94, 0.96 | Glassworks identity color (pale cyan band, roof ties, pane rack) |
| depot_accent | 0.92, 0.20, 0.24 | Depot trim and its trucks' cabs (a bright red) |
| steel_accent | 0.36, 0.52, 0.74 | Steel Mill roof and band (a steel blue) |
| gas_accent | 0.56, 0.86, 0.84 | Electrolyzer cell tops and hydrogen tank (a pale aqua) |
| fuel_accent | 0.94, 0.42, 0.18 | Fuel Plant bands and loading arm (a rocket red-orange) |
| chemical_accent | 0.62, 0.38, 0.80 | Chemical Plant reactor cap, bands, and drums (a violet) |
| aluminum_accent | 0.74, 0.80, 0.90 | Aluminum Smelter roof and ingots (a pale silver-blue) |
| furnace_accent | 0.30, 0.60, 1.00 | Electric Furnace band, electrode collars, transformer (an electric blue) |
| brick | 0.58, 0.30, 0.24 | Steel Mill brick hall and chimney |
| forester_accent | 0.36, 0.72, 0.30 | Forester shed roof and water-tank band (a leaf green) |
| quarry_accent | 0.44, 0.54, 0.66 | Quarry face band and crane hoist (a slate blue-grey) |
| kiln_accent | 0.68, 0.34, 0.22 | Charcoal Kiln's brick domes |
| tilled | 0.42, 0.30, 0.20 | The Forester's tilled plot |
| motherlode_rock | 0.30, 0.28, 0.27 | A motherlode's dark rock mound |
| fabricator_accent | 0.96, 0.56, 0.16 | Fabricator press, gantry, and band (a machine orange) |
| electronics_accent | 0.24, 0.72, 0.38 | Electronics Works band, annex, and spools (a circuit green) |
| assembly_accent | 0.20, 0.32, 0.70 | Assembly Plant band, crane rail, and bay door frame (a deep blue) |
| shipyard_accent | 0.94, 0.95, 0.96 | Shipyard hall, gantry, and the ship's hull (a launch white) |
| cement_accent | 0.80, 0.76, 0.66 | Cement Kiln drum and band, Concrete Mixer silo and drum band (a pale grey-beige) |
| lithium_accent | 0.78, 0.70, 0.92 | Brine Well pump-house roof, band, and wellhead (a pale lilac) |
| fab_accent | 0.42, 0.40, 0.92 | Semiconductor Fab band, door frame, and tank cap (a clean violet-blue) |
| nuclear_accent | 0.74, 0.90, 0.20 | Enrichment Plant bands and cask bands (a hazard yellow-green) |
| brine | 0.86, 0.92, 0.96 | The Brine Well's brine pond |
| battery_accent | 0.30, 0.82, 0.40 | Battery Bank cabinet bands (a charge green) |
| reactor_accent | 0.46, 0.42, 0.90 | Fission Reactor bands (a radiation blue-violet) |
| sun | 1.00, 0.84, 0.30 | The sun icon |
| moon | 0.86, 0.89, 0.96 | The moon icon |
| icon_mono | 0.96, 0.96, 0.96 | Icons the UI tints by state (`power_switch`, `network`, `speed`): a light neutral, so a tint shows true |
| research_accent | 0.36, 0.86, 0.66 | Research Lab band and awning (a mint green) |
| gravel | 0.56, 0.51, 0.43 | Road surface |
| gravel_edge | 0.43, 0.39, 0.32 | Road verges, pebbles, and slab sides |
| bridge_wood | 0.56, 0.39, 0.23 | Bridge planks and rails |
| bridge_rail | 0.40, 0.27, 0.16 | Bridge posts, stringers, piles, and plank edges |

Crown greens are mid tones with no near-black shade: a crown's shaded side would otherwise read
as black under the dark ambient light. Crowns shade light on upward faces and dark underneath.

Emissive materials glow at half their base color. Mountains keep their own stone and snow bands
(see terrain.md), drawn from the same tones.

### Scale

- 1 fine cell = 1 unit; 1 map cell = 4 x 4 units.
- The newer deposits' field models (resource_gen.py) match their item icons: Bauxite reddish-brown
  clay lumps with pale streaks; Quartz a grey rock with milky six-sided points; Limestone stacked
  cream slabs; Sulfur a yellow crust with bright knobs over grey rock; Titanium Ore dark rock with
  metallic blue-grey veins; Rare Earths pale rock flecked pink and violet; Uranium Ore dark rock
  with faintly glowing yellow-green veins (`uranium_glow`); Crystals tall violet shards with
  glowing tips (`crystal_light`).
- Size targets: the player about 1.4 tall, deposits (Iron Ore and Copper Ore about 0.8, Coal 0.6,
  Stone 0.9; Crude Oil is a flat pool about 0.05 high), trees 2.5 to 3.5 (by field size),
  mountains 3.2 to 3.5, buildings 1.2 to 2.2 (see buildings.md; the Deep Well's derrick about
  3.1 and the Deep Mine's headframe about 3.3), a truck 1.6 long, 0.8 wide, and about 0.85 tall.
- **Leveled buildings** (the level models, `<family>_<level>`): every level of a family stands on
  the same 4 x 4 plot, and the levels read as one building growing, not different buildings. Refits
  scale today's model up a little (1.2x from 2 x 2, 1.1x from 3 x 3, never more than 20%) onto the
  plot, the yard dressed with the family's props (ore heaps, log piles, sand, crates, pipes); a
  level 1 smaller than today's is 0.75x. Each family keeps its accent through its levels, and a
  powered level carries a power pole. The standalone models the levels replaced (the Mine, Lumber
  Camp, Water Pump, Sand Collector, Oil Well, Smelter, Charcoal Kiln, Drill Rig, Deep Mine, Lumber
  Mill, Pumping Station, Sand Dredge, Deep Well, Steel Mill, Electric Furnace, Cement Kiln, and
  Tile Kiln), and the dropped Aluminum Smelter and Air Separator, are no longer exported; their
  descriptions below stay as the parts the level models are built from.
- The tier-1 refining buildings are bigger than the tier-0 start and mostly long: Steel Mill (4 x
  6, a brick hall with a tipped converter and chimney at the back, a casting bay, billets at the
  front), Electrolyzer (4 x 5, two rows of cells, a rectifier house, an oxygen and a hydrogen tank
  at the front), Fuel Plant (4 x 5, two process columns, a blending tank, a loading arm), Chemical
  Plant (5 x 6, a reactor and distillation tower, a pipe rack, a control house, drums), Aluminum
  Smelter (3 x 8, a narrow pot line under a saw-tooth roof with bus bars and a fume duct), Electric
  Furnace (4 x 4, a round arc furnace with three electrodes, a transformer, a glowing tap); each
  powered one has a power pole.
- The renewables: Forester (a fenced tilled plot with a potting shed, a water tank, and a power
  pole; the trees are its field), Quarry (stepped benches cut into a block at the back, a derrick
  crane, cut blocks at the front), Charcoal Kiln (two domed brick kilns, logs, a charcoal heap),
  Rock Crusher (a hopper over a jaw crusher, a conveyor to a sand pile), Deep Well (a tall derrick
  over a capped wellhead on an oil-stained pad, a pump motor, a tank), Deep Mine (a steel headframe
  with a winding wheel over a shaft, a winding house, an ore skip to a tipple). Sites:
  `worked_out_oil` (a flat dry brown stain), `worked_out_pit` (a shallow pit with a spoil rim),
  `motherlode_base` (a ring of dark boulders round a flatter middle, 8 x 8).
- The parts buildings: Fabricator (4 x 6, a high hall with a press frame at the back, a roller line
  under a gantry crane down one side, plates, beams, and crates at the front), Electronics Works
  (4 x 5, a low building under a saw-tooth glass roof, a clean-room annex, wire spools at the
  front), Tile Kiln (3 x 7, a long brick tunnel kiln with a chimney at the back end, a car track
  down its length, white tile stacks at the front; `kiln_accent`), Assembly Plant (6 x 8, a large
  hall with a tall bay door at the front, a crane rail on the roof, a finished thruster on a stand
  outside), Shipyard (8 x 12, a fabrication hall across the back half and a launch pad on the front
  half with a service gantry beside it). Props: `display_stand` (a short post on a round plinth,
  about 0.6 high, `metal_dark` with a `power_accent` rim), and the ship in parts for the Shipyard's
  pad, stacking 1 unit apart: `ship_engine` (a thrust frame over two bells, about 1.0 high),
  `ship_hull_ring` (a white ring 1.0 high with a dark band), `ship_nose` (a white cone with a
  window band, about 1.6 high).
- The power buildings: Solar Array (6 x 6, three rows of tilted dark-blue panels ruled in silver on
  low steel legs over bare earth, an inverter with a `power_accent` band at the front), Battery
  Bank (3 x 4, two rows of white cabinets with `battery_accent` bands and green lamps, a finned
  transformer at the front; static), Fission Reactor (6 x 8, a hyperboloid concrete cooling tower
  at the back with a white lip and a dark open top, a domed round reactor building, a turbine hall
  across the front, an intake pipe). Icons (`assets/models/icons/`): `sun` (a pale yellow
  disc with eight rays), `moon` (a pale crescent), `power` (a lightning bolt in
  `power_accent`), `power_switch` (the power symbol: a ring broken at the top by a vertical bar),
  `network` (a hub mast on a base with a ring around its top and a round cap) and `speed` (a
  gauge: a half dial with three ticks, its needle pointing right for fast, from a round hub), flat
  shapes facing the game camera. The last three are `icon_mono`, a light neutral the UI tints by
  state
  (`Good` green on and online, `TextDim` off, `Warning` offline; ui-style.md); the green of
  `Good` is the hubs' `hub_accent`. The detail panel's truck icon is the `units/truck` model
  itself, drawn tight like an item icon.
- Infrastructure (`assets/models/infrastructure/`, 2 x 2 tiles): road pieces are a gravel slab 0.03
  high with a darker verge along each closed edge and three pebbles at the same spots on every
  piece (patch, end, straight, corner, tee, crossing, authored open North first, then East); a
  bridge deck is planks across a span running North-South 0.14 up, on two stringers, with a rail on
  three posts along each side; the ramp slopes it down to the North; a pier is two piles into the
  water under a cross beam.
- The Research Lab (4 x 5, a low white lab with a mint band, a glass-roofed bay at the back, a
  slotted dome and a small dish on the roof, an instrument bench under a mint awning at the front).
  Project icons: `material_transfer` (a crate under a `hub_accent` arrow arcing over it) and
  `advanced_material_transfer` (two crates under two arrows, the inner with a `power_accent`
  band).
- The later tiers: Sawmill (3 x 5, an open-sided shed with a `lumber_accent` gabled roof over a
  saw bench with a round blade, logs at the back, plank stacks either side at the front), Cement
  Kiln (3 x 7, a sloped rotary drum on piers with tyre rings from a preheater tower and chimney at
  the back down to a glowing firing end, a fuel hopper, clinker heaps at the front), Concrete Mixer
  (3 x 4, a cement silo raised on legs, a tilted mixing drum, a chute to a wet heap at the front),
  Brine Well (4 x 6, a pump house and wellhead at the back, two bermed evaporation ponds down the
  front, the nearer drying to white, a salt heap), Semiconductor Fab (5 x 6, a white clean-room
  block with a raised back, air handlers and fans on the roof, a loading door, a chemical tank),
  Air Separator (4 x 5, a compressor house, two tall banded cold boxes, a frosted oxygen tank on
  saddles at the front; `gas_accent`), Enrichment Plant (6 x 7, a concrete containment dome at the
  back, a cascade hall with rows of centrifuges at its open front, a cask pad at the front).

### Ground levels

- Land surfaces sit at y = 0, with relief under 0.04 and none within about half a unit of a
  cell edge. Water and lava surfaces sit at y = -0.1.
- Land carries a short skirt from its surface down to -0.1 on every open side. Between two land
  cells it is hidden; beside water or lava it shows as a bank, so a liquid cell never needs
  geometry that depends on its neighbors.
- Transition pieces sit 0.006 above the ground they cover, with walls down to -0.1, so they stay
  above the tile beneath without a visible gap.
- Flat ground pieces cast no shadows; standing things (mountains, buildings, resources) do.

### Cell-sized models

- A cell-filling model is 4 x 4 units, origin at the center of its base, and stays inside its
  cell.
- Its edges either lie flat on the ground (ground tiles, mountains) or follow a profile shared by
  its family (the river channel: a 2-unit channel centered on each connected edge), so any variant
  fits beside any other in any 90-degree rotation.
- Pieces that attach to one edge or corner (transition strips and corners) are authored for the
  North edge or the North-West corner and turned into place.

### Character

- The player is a colonist in an orange work suit (`character_gen.py`), about 1.4 tall, authored
  facing +Z (south) with its origin between the feet. A light gray reflective stripe circles the
  chest; the belt, gloves, and boots are `boot`; the legs and hips are the suit a shade darker; a
  bare head with brown hair, two dark eyes, and a nose. The head is about a fifth of the height
  so the figure reads at the default zoom.
- Rigid parts, nothing bends: each part (hips, torso, head, upper arms, forearms, thighs, shins)
  hangs from a joint and moves with it whole, so the model is a tree of nodes, not a skinned mesh.
- Two looping clips: `idle` (3 seconds, a slow breath and a slight arm sway) and `run` (0.6
  seconds, two strides at the player's 6 units a second, arms swinging opposite the legs).

### Item models

- Each resource has one item model (`items/item_<resource>.glb`, from `item_gen.py`): a single
  chunky object about 1 unit across, resting on y = 0, drawn only into the UI's resource icons.
- Items are built to read as silhouettes at about 22 pixels, on the dark UI panel: few large
  facets, one clear shape each (a log stack, a lump, a barrel, a droplet, ingots, a canister).
  Very dark materials (coal, crude oil, charcoal, rubber, carbon fiber) get lighter upper facets,
  parts, or mid-gray colors so they do not vanish into the panel. The two ores reuse their
  fields' rock and vein colors so they match the map.
- Related items share a shape family and differ by color, so the catalog reads as a set: ore
  lumps (the ores, with glowing veins for Uranium Ore), six-sided crystal clusters (Quartz,
  Crystals), heaps and blocks (Sulfur, Lithium, Limestone), canisters and tall gas bottles (the
  fluids), crossed ingots (the metals), stacks of plates (Hull Plate, Composite Panel, Ceramic
  Tile), spools (Copper Wire, Insulated Cable), rolls (Carbon Fiber), and sacks (Cement,
  Plastic). Components and parts each get a shape of their own (a gear, a motor, a chip, a dish,
  a bell nozzle).
- An item whose identity is its side profile may lean away from the game camera so the icon,
  drawn from that camera's angle, shows the profile: the water drop leans its tip back 25
  degrees.

### Variants and rotation

- Three variants (a, b, c) where repetition shows: ground tiles, mountains, ore, trees. A map
  cell's variant and a 90-degree yaw come from a hash of its coordinates, so the world looks the
  same every run.
- One model where a thing has a single identity: each building, each river shape, each
  transition piece, each resource item.

### Sources

- Assets are generated by the asset factory first (`src/asset-factory`, docs/asset-pipeline.md):
  geometry is code, so a style change is a parameter change and a regeneration.
- CC0 imports are allowed when they match the look; their folder README notes the source. There
  are none today: every model, the character included, is generated.

### Naming and loading

- File names are `snake_case` by family: `ground_grass_a.glb`, `edge_mouth_sand.glb`,
  `river_bend_sand.glb`, `mountain_snow_b.glb`, `smelter.glb`.
- Models are required: the game loads them through `ModelLibrary`, and a missing model throws with
  its path. The character is the only exception: without it the player is a capsule (see
  player.md).

## Related

- terrain.md (terrain models), buildings.md (building models and the build ghost), resources.md
  (ore and tree models), presentation.md (`ModelLibrary`), docs/asset-pipeline.md (making assets).
