# Clothing-specific gentle walking

All 21 outfits have individually authored landmarks and stride amplitudes in `代码/walk-profiles.csv`, shared by Windows and macOS packaging.

Long skirts keep the garment connected and move the exposed shoes only. Short skirts expose a longer articulated leg region. Trousers bend continuously from the upper leg, with small knee flexion and alternating foot lift. The ball gown stays intact and allows only tiny shoe tips behind the hem. Heavy coats and wide trousers use smaller strides. Existing stop/start easing remains.

The renderer displaces narrow horizontal strips with zero displacement and zero slope at the attachment, replacing the old rigid half-image rotations. The original upper body remains unchanged. Windows caches the intact body layer for each outfit; the web renderer caches it by image.

Validation: compile Windows, run its movement self-test, and compile tests/MotionReview.cs together with WalkMotion.cs and the embedded CSV. The review checks all 21 outfits at rest and four walking phases, asserts original rest pixels and unchanged upper-body pixels, requires visible motion for every outfit, and saves a contact sheet for visual inspection. macOS source and packaging syntax are checked on Windows; the macOS application itself must be checked on a Mac.

## Horizontal views

All 21 outfits now have a separately drawn right-facing profile in 图像素材/人物/侧面装扮, sourced from 侧面原图.png. The asset preparation tool slices the 7-by-3 atlas and normalizes each profile to a 177px height. Leftward walking mirrors the side sprite; upward/downward input restores the front view. Idle retains the last view, and diagonal input with a horizontal component uses the side view. Outfit changes select the matching side sprite.

Side gait layers the farther leg behind the near leg and uses continuous small forward/back steps. Long garments keep their clothing-specific covered leg regions. Windows resources and macOS packaging include all side assets. The movement self-test checks left/right orientation, idle retention, vertical reset and asset availability; MotionReview accepts a third argument to check side rendering.

## Back view and clearer steps

Upward input selects the matching back sprite for all 21 outfits; downward input selects the front sprite, with horizontal and diagonal input retaining side-view priority. Back assets are packaged on both platforms and checked by the direction self-test.

Stride travel is increased per garment. Swing-foot lift follows a quarter-cycle offset from forward travel, with continuous knee bending and visible landing extension. Long skirts expose alternating shoe steps; the ball gown draws small shoes at its hem. Body bounce and a small weight shift follow the gait, and cadence is slightly slower. Scanline overlap follows vertical deformation to keep stretched feet connected.

Validation: Windows build and direction/movement self-test; all 21 outfits rendered at five phases in front, side and back views (315 poses); JavaScript and macOS packaging syntax checks. macOS runtime itself was not executed on this Windows machine.

## Consistent proportions and planted side steps

Side and back atlases were redrawn to match the original front-view head/torso/leg proportions, with reduced side-face and hair volume. Atlas slicing follows the main connected silhouette to exclude neighboring fragments. All views remain at the same 177px standing height.

Side gait now keeps feet close to ground level, with alternating forward/back leg swing instead of the previous marching lift. Pixel-rounded displacement reduces shimmering; exposed calves deform from their attachment below the hem. Short-skirt and trouser pose checks now require actual movement above the shoes. Front/back lift and body bounce were reduced. This is an original gait inspired by the user's Stardew Valley reference, using the project's own artwork.
