# Contour4 Valuables 0.2.4

Independent companion mod for the Contour4 map. R.E.P.O. 0.5.2, REPOLib 4.5.1, BepInEx 5.4.2305, Unity 6000.3.17f1.

The map declares the hard BepInEx dependency `spacejourney.contour4.valuables` (minimum 0.2.0). For this local candidate use companion 0.2.4 in the selected test profile. This companion does not depend on the map and does not insert its pickups in vanilla random loot pools. Both peers need identical companion/map versions.

Version 0.2.2 uniformly doubles only the control-sample authored hierarchy at runtime to .24 / .60 / .24 m, preserving its .7 kg mass, price and fragility. Glass, handle and all fourteen colliders scale together around the bottom pivot. Root-space mesh bounds drive size validation, center of mass, force anchor and RoomVolumeCheck. The capsule uses Medium because .60 m exceeds the Small category height of .50 m; explicit host spawning remains intentional. The other seven hierarchies retain their transforms and settings. The AssetBundle is byte-identical to 0.2.1; editable Unity prefabs retain their authored scale and the runtime applies the capsule multiplier. Exact-candidate runtime evidence is recorded separately.

Version 0.2.1 restores the native `PhysGrabObjectCollider` bridge on each enabled solid authored collider before activation. Native initialization still assigns collider IDs and parent links. All eight models, bundle bytes, catalog settings, spawn keys and public API are unchanged. This is a local pickup hotfix candidate; actual native input pickup, transport, sale and multiplayer require separately recorded game checks.

Install the package through a mod manager, or copy `plugins/Contour4Valuables` into an explicitly selected development profile's `BepInEx/plugins`. This source task does not install into an owner's existing profile or publish the package.

| Key | Unity X/Y/Z m | Mass | Initial value | Fragility / durability |
|---|---|---:|---:|---:|
| experiment-cassette | .24 / .08 / .16 | .8 kg | $350–650 | 25 / 90 |
| control-sample | .24 / .60 / .24 | .7 kg | $1200–2200 | 90 / 20 |
| sensor-rod | .22 / 1.65 / .22 | 2.4 kg | $1000–1800 | 50 / 65 |
| calibration-weight | .33 / .30 / .30 | 9 kg | $650–1100 | 25 / 90 |
| portable-gravimeter | .46 / .34 / .32 | 3.2 kg | $950–1600 | 50 / 65 |
| gimbal-gyroscope | 1.18 / .76 / .55 | 8 kg | $2300–3800 | 50 / 65 |
| gravity-lens | 1.35 / .95 / .52 | 14 kg | $4200–6400 | 70 / 40 |
| mobius-strip | .52 / .43 / .22 | 2.1 kg | $1800–3000 | 50 / 65 |

Durability values are prototype balance, pending the owner's equal-impact and cart playtest. Mass and price ranges follow `../openspec/changes/contour4-procedural/valuable-catalog.md`. Native `ValuableObject`, physical pickup, impact, damage, value sync and Photon behavior are cloned from the installed game's Trophy at runtime. No game asset or DLL is redistributed.

`Contour4Valuables.ValuableApi` exposes `IsReady`, `Keys`, `GetPrefab(key)` and `Spawn(key, position, rotation)`. All templates register synchronously in companion `Awake`; `IsReady` becomes true only after all twenty network refs are valid. The map's hard dependency guarantees plugin order. Photon IDs are `Valuables/Contour4 <key>`. Only the host may call Spawn. It calls REPOLib's normal `Valuables.SpawnValuable`, which uses `PhotonNetwork.InstantiateRoomObject` in multiplayer. The host initializes a catalog price before native Start; native value synchronization remains intact.

The pivot is bottom-center, local Y up. Spawn positions represent the support surface plus clearance. Map markers own placement, local room gravity/orientation, duplicate prevention and clearance/route checks. Catalog roles: cassette on observation operator console shelf, capsule upright in sample-storage cell, rod upright in open observation gallery/shaft landing rack, weight on calibration lower shelf/pallet. This companion spawns nothing automatically.

## Version 0.2.4 — cryostat and gravity laboratory

Adds the approved cryostat V5 and ten textured laboratory instruments (#22–31): twenty network prefabs total. The public API, original nine keys/settings, capsule runtime multiplier, host-only explicit spawning and required REPOLib dependency remain intact. The #09 interferometer is still excluded. No automatic random-pool enrollment or duplicate spawning is added.

| Added key | Unity X/Y/Z m | Mass kg | Initial value | Fragility / durability |
|---|---|---:|---:|---:|
| anomaly-cryostat | 1.184 / 2.515500 / .994 | 22 | $38000–52000 | 70 / 40 |
| corner-cube | .080 / .043644 / .079671 | .25 | $450–800 | 70 / 40 |
| free-fall-photogate | .132266 / .074476 / .140 | .65 | $700–1200 | 50 / 65 |
| pirani-gauge-head | .120 / .055563 / .054890 | .4 | $500–900 | 50 / 65 |
| bullseye-level | .090 / .017713 / .089907 | .18 | $350–650 | 70 / 40 |
| atwood-machine | .521169 / .800 / .462122 | 4.5 | $1800–3000 | 50 / 65 |
| kater-pendulum | .537570 / .950 / .539031 | 5.5 | $2200–3600 | 50 / 65 |
| vertical-seismometer | .500 / .278802 / .376088 | 3.2 | $1500–2600 | 70 / 40 |
| hydrostatic-balance | .550 / .488389 / .282737 | 3.8 | $1700–2900 | 70 / 40 |
| vacuum-drop-column | 1.259749 / 1.750 / 1.135839 | 18 | $16000–24000 | 70 / 40 |
| cavendish-balance | 1.800 / 1.084415 / .822892 | 12 | $12000–19000 | 70 / 40 |

The ten laboratory mass/price/durability settings are new prototype balance proposals. Cryostat mass/value/high fragility follow the catalog. Its exact approved silhouette fits inside the catalog envelope 1.25 / 2.55 / 1.10 m (Unity XYZ); no axis stretching was applied. Other new dimensions preserve the approved source bounds exactly. Tall/Wide metadata on large items is descriptive: the map must validate authored sites and routes using the actual bounds.

Native approved sources and all previous nine generated assets are byte-preserved. The new mesh transfer preserves every source triangle, loop normal, UV and material identity. Only parts sharing the same material are concatenated; cryostat retains 15 material groups instead of 184 separate renderers. Original source maps are retained: #10/#30/#31 use 1024, #22–29 use 512. Standard uses sRGB color, linear normal and metallic R / smoothness A. Separate transparent materials retain the authored laboratory tints/alpha. Cryostat Blender transmission1/IOR1.45 is approximated by Standard premultiplied alpha .16, with source optical metadata preserved in its export receipt; this is not a refractive shader.

Primitive compounds follow supports and contact surfaces, leaving photogate channel, cryostat rails, Kater arch/grip, seismometer grip pockets, vacuum-column tripod gap and Cavendish handles clear. Native PhysGrabObject.Start populates its collider Transform list and IDs; bridges are installed before activation and stale template entries cleared. The root and authored hierarchy use the native grab layer.

Actual Unity 6000.3.17f1 build and frozen-bundle audit passed all 20: exact bounds/bottom pivots, texture flags/sizes, triangle/normal/UV transfer, contact extents, open-grip probes, catalog masses and six-second Physics.Simulate settling. New real Unity renders are in evidence/<key>-unity.png. For all four new glass items a contrast target was placed immediately behind a real source glass triangle in the packed prefab: measured contrast retention .6560 cryostat, .5913 seismometer, .3519 hydrostatic balance, .4334 Cavendish (gate >.35). These are isolated-pane shader checks, not a claim of full optical simulation.

Frozen bundle SHA256: D78B49ABD2E630CFAE264F8CE9F8CBBF7673B1C0FA6C24C3AB1593D5005B2635. Plugin build passed with zero warnings/errors. Installation, game input pickup, transport, damage, extraction, two-client behavior and owner acceptance remain unrun for this exact candidate. Source and runtime ZIPs are local deliverables; no game/profile modification or publication is performed.

## Editable Unity project

Open `Unity/` with Unity **6000.3.17f1**, then open `Assets/Generated/ValuablesReview.unity`. Each item's editable prefab, native Mesh `.asset`, Standard materials and primitive/segmented convex compound colliders are in `Assets/Generated/<key>/`. Authored transfer sources and three RGBA 512 maps for #01–08; #11 retains 1024 maps are in `Assets/Source/`. Source Blender files are preserved in `Native/` and in the originating project.

Color is imported sRGB. Metallic R + smoothness A is linear; normal maps use OpenGL +Y without green flipping. The capsule has an independent transparent glass mesh/material and opaque inner stone; glass does not cast an opaque shadow. Weight recesses and capsule handle use compound boxes, preserving grip space. Mesh normals and UVs are preserved; Unity recalculates tangents. The FBX is included as a conventional editable transfer; the consumed meshes use a deterministic loop-normal/UV native import to preserve the baked atlas exactly.

Run `tools/export.py` with the pinned Blender executable to repeat exports, then `BuildValuables.Build` via Unity batch mode to import, validate, render, simulate settling, and build the bundle. Run `tools/build.ps1` to compile the companion and package the exact native bundle. Unity licensing/service access must work under the selected execution context; a sandbox licensing failure is not proof of an unlicensed host.

## Verification status

`evidence/unity-report.json` records target-size/pivot checks, triangles, winding consistency, renderer/collider counts and 6-second physics drop/settle checks. `evidence/*-unity.png` are real Unity renders, not concept images. `evidence/dotnet-build.log` is the actual game-API compilation result. `evidence/package-receipt.json` binds distributable hashes. Game pickup, extraction, fragile capsule balance and two-client behavior require actual runtime/owner evidence; Unity settling does not establish those outcomes.

Original assets were authored for Contour4 from the owner's approved concepts and Blender prototypes; this package contains the approved authored and owner-supplied Meshy meshes/textures. Boobees Valuables was consulted for runtime integration patterns, not copied assets. Harness2 Propose-Probe-Compose was applied methodology-only: preserved original baked assets, bounded import/preset/collider candidates, actual same-size/render/settle/compile probes and retained evidence. No Harness2 benchmark/framework execution is claimed.

## Version 0.2.0 batch 02

The four additional approved assets come from `../prototype/valuable-models/batch-02/textured/`. Their exact optimized triangle counts are 1964, 3932, 2708 and 2028. `Native/05..08` preserves the editable Blender sources; `Unity/Assets/Source/05..08` preserves FBX, loop-normal/UV mesh transfer and all three unchanged 512 PNGs. The original four Native/source/generated assets and both 0.1.0 ZIPs remain byte-identical (see `evidence/baseline-preservation.json`). Run Blender export with `-- --batch2` to regenerate only these four transfers.

Lens high fragility is the disclosed prototype setting **70 / 40**, between medium **50 / 65** and very high **90 / 20**. The optical disc alone uses separate transparent premultiplied Standard glass; the frame stays opaque with the baked atlas. `evidence/gravity-lens-transmission-unity.png` shows a real contrast target through the glass; `lens-transmission.json` compares the same camera with/without glass and target. The new shapes use compound contacts around the visible frame/grips, segmented convex ring/ribbon contacts and a narrow convex glazing disc. There is no full-volume invisible box across the Mobius center or the gyro frame.

`unity-report.json` additionally proves new collider bounds stay inside visual catalog extents, open grab spaces are clear, the glazing center is solid and its AABB corner outside the disc is clear. All eight undergo actual Physics.Simulate settling with catalog mass. Edit Mode contact probes run after physics actor initialization; transient uninitialized convex bounds are not treated as physical evidence. The review renders were inspected directly. Native bundle reload checks all eight prefabs. Assetbundle and jsonserialize modules remain enabled, and the bundle audit checks PersistentTypeID 142 (AssetBundle v3).

This companion spawns nothing itself. The map selects the gravimeter for observation desk/control shelves; gyro for gravichamber, calibration stand or clean bridge area; lens for observation, gravichamber or cargo support; Mobius for laboratory or observation display. Placement, route and mandatory companion dependency belong to the map integration. The public ValuableApi signatures and BepIn GUID are unchanged. Multiplayer and owner acceptance require separate actual evidence.

## Version 0.2.3 — approved field core

Adds `field-core` as the ninth runtime key: `Valuables/Contour4 field-core`, authored bundle path `assets/generated/field-core/field-core.prefab`. Existing eight keys, native grab bridges, capsule 2x runtime scale and public API signatures are preserved. Native #09 interferometer and #10 cryostat remain editable approved sources only; they are not registered in this package.

The owner's approved Meshy source is `../prototype/valuable-models/meshy-field-core-15000/field-core-15000.blend`, SHA256 `e38640f43e1d38746dd0f68bb9ecdf0b4f37c8354a1a8b431081908a80162b21`. Its 14,944 triangles, UVs, asymmetric shape, four packed 1024 maps and material identity are preserved. A single uniform factor **0.9557276029017425** fits the catalog depth envelope; actual W/D/H is **1.7059345 / 1.5500000 / .9693324 m**, within the unchanged catalog envelope **2.35 / 1.55 / 1.35 m**. The catalog dimensions are an envelope, not three independently stretched axes. Source supplied by owner under their Meshy account terms; no new third-party redistribution license is asserted.

Runtime mass **36 kg**, price **$48,000–65,000**, low fragility/durability **25 / 90**. Bottom-center pivot; native Rigidbody and force anchor have a low mass center at **.315033 m** above the base. Eighteen primitive box colliders cover the base, winding body, cap, coil housings, ribs, control panel and lifting-eye borders. Probe checks keep both low lifting-eye centers clear and confirm contact rays from the top, side and front. Native `PhysGrabObjectCollider` remains on every enabled non-trigger authored collider before activation, with parent/ID initialization through the base game.

No stock native `ValuableVolume` category fits this horizontal core's 1.55 m depth (Wide/Tall depth is 1.20 m). Wide is descriptive metadata only; no vanilla random pool enrollment is used. Explicit host spawning must check the exact geometry/room bounds and route. The companion alone spawns nothing; shaft placement and route validation are owned by the map package.

`tools/export-field-core.py` deterministically adopts and exports only #11. Raw color and normal PNG bytes are unchanged; metallic R / smoothness A is composed in linear space from original metallic and roughness. Unity uses sRGB color, Non-Color linear mask and tangent normal with no green flip; all three consumed maps remain 1024. The approved source has 254 triangles whose averaged smooth corner normals oppose geometric normals; export preserves this exact measured **14690/14944 = .983003212** baseline (opposing area .50639%) rather than rewriting approved shading. Existing procedural assets retain their prior winding gate.

Actual Unity 6000.3.17f1 renders, bundle creation/reload, dimensional/pivot checks, 6-second dynamic settling at 36 kg and contact checks are recorded in `evidence/`. The packed audit reruns all nine authored prefabs. Capsule 2x is still applied only by the runtime loader. Actual game pickup, map transport/sale, damage balance, two-client behavior and owner acceptance require their separately recorded probes.