# Native sources: selected frame #09 and cryostat #10

`09-interferometer-frame.blend` is the owner's selected Meshy frame adopted as the editable source for the set. The existing #01–08 sources remain unchanged. This adoption adds no Unity assets, colliders, spawn configuration, build or runtime integration; the companion's eight-item runtime remains the existing version.

The source is `../../prototype/valuable-models/meshy-optical-device-12000/optical-device-12000.blend` (SHA-256 `3bc7c29b83ac83717bebe87a8e532788c629183b2aebd7a607827d6999318c21`), already optimized from the owner's supplied Meshy archive. Source asset rights remain those of the supplied asset and owner's account terms. The prior procedural frame in `../../prototype/valuable-models/batch-03/textured/09-interferometer-frame/` is retained as history.

The approved shape is uniformly scaled by **1.469703586144337**, with width fitted to 2.80 m. Actual Blender X × Y × Z extents are **2.800000 × 0.580113 × 0.940155 m**. The catalog envelope remains **2.80 × 0.80 × 1.20 m** for future gameplay checks. Depth and height have not been stretched to fill it. The Native file uses meters, Blender Z up, identity object transforms and a bottom-center origin: bounds X ±1.399999976, Y −0.290056348…+0.290056378, Z 0…0.940155149 m. Unity axis conversion and placement remain future integration work.

Blender **4.5.14 LTS** reopening checks confirm **12,000 actual triangles**, 5,778 vertices, unchanged topology and UV coordinates, and the same material graph. Every vertex matches the source's uniform world transform within **1.27 × 10⁻⁷ m**. Custom split normals are retained; Blender re-encoding after the transform introduces a measured maximum component difference of **7.70 × 10⁻⁵**. A real temporary Cycles render was inspected with the linked shaders.

All four original 1024 × 1024 PNG maps remain packed and byte-identical; no rebake or texture processing was performed. Base color is sRGB; metallic, roughness and tangent normal are Non-Color, with the original shader connections preserved. Their SHA-256 identities are:

| Map | SHA-256 |
| --- | --- |
| basecolor | `2f9497ec842acc4cb0af3bb712b5f696ecb8c72be7f2608b61ffc1021e79400e` |
| metallic | `bae065d2391399af39b4d01e6aa13c717e97934e3b705139a259c8d3ce26d561` |
| roughness | `2db540025b1df9a7a85cc4ca6ec87f6af4237cfb056c96ce5d9f393f2463254f` |
| normal | `5b2beb8ed11a8287de7e9449d8f889b6b0ef336b09f84b5e333d735c9c0f151e` |

Native #09 SHA-256: `5e07f1d4ddfbb05230c471aee18ca8cdb618c17147bb9662d2b3ad1bfb46ed44`. The source optimized package and previous procedural #09 are unchanged.

`10-anomaly-cryostat.blend` is the owner's selected textured V5 cryostat, derived from `../../prototype/valuable-models/meshy-cryostat-12000-v5/cryostat-12000-v5.blend` (source SHA-256 `a45969ad3b156c0e9a3f1bd8c668efc6450f92f4165d85cb709c1104ed1317e5`). The Native copy has every top-level object translated uniformly by **−0.009 m on Z** to put the base at zero; the source bottom was at +0.009 m. Mesh geometry, materials, UVs, and packed images were preserved exactly; relative placement is unchanged. Its actual dimensions are **1.184000 × 0.994000 × 2.515500 m**, inside the **1.25 × 1.10 × 2.55 m** catalog envelope, centered in X/Y and Z-up. Blender 4.5.14 reopened this exact Native copy with **184 mesh objects and 11,602 evaluated triangles**. All **62 packed images** yielded embedded bytes after reopening from Native, independent of the source model directory. The 10 baked material sets retain image links for base color, metallic, roughness and normal; the separate clear-glass Principled shader retains transmission 1 and IOR 1.45. The V5 source's donor provenance, wear method and reuse terms are documented in its [source README](../../prototype/valuable-models/meshy-cryostat-12000-v5/README.md). The previous textured procedural cryostat remains in `../../prototype/valuable-models/batch-03/textured/10-anomaly-cryostat/` as history. This adoption selects the Native source only; no Unity, spawn, package, build or runtime integration is included.

Reproduction script, checks and temporary render are retained in `%TEMP%/contour4-adopt-meshy-frame`. Harness2 Propose–Probe–Compose was applied as methodology-only, using preserved baseline and actual probes; its Python framework was not invoked because no suitable mesh-task adapter was available.

## Approved source #11 — field core

`11-field-core.blend` is the approved optimized Meshy generator adopted through `tools/export-field-core.py`. Source SHA256 `e38640f43e1d38746dd0f68bb9ecdf0b4f37c8354a1a8b431081908a80162b21`; uniform factor .9557276029017425; Blender XYZ 1.705934525 / 1.549999952 / .969332397 m, Z up, bottom-center origin. Topology remains 14,944 triangles and all four original 1024 maps remain packed byte-identical. No stretch, mirroring, decimation, repaint or rebake. The source's original corner-normal baseline is retained. #11 is registered in companion 0.2.3; #09/#10 remain Native only and their files are untouched. Provenance and licensing remain those of the owner's supplied Meshy source.
