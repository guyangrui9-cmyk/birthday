# Clothing-specific gentle walking

All 21 outfits have individually authored landmarks and stride amplitudes in `代码/walk-profiles.csv`, shared by Windows and macOS packaging.

Long skirts keep the garment connected and move the exposed shoes only. Short skirts expose a longer articulated leg region. Trousers bend continuously from the upper leg, with small knee flexion and alternating foot lift. The ball gown stays intact and allows only tiny shoe tips behind the hem. Heavy coats and wide trousers use smaller strides. Existing stop/start easing remains.

The renderer displaces narrow horizontal strips with zero displacement and zero slope at the attachment, replacing the old rigid half-image rotations. The original upper body remains unchanged. Windows caches the intact body layer for each outfit; the web renderer caches it by image.

Validation: compile Windows, run its movement self-test, and compile tests/MotionReview.cs together with WalkMotion.cs and the embedded CSV. The review checks all 21 outfits at rest and four walking phases, asserts original rest pixels and unchanged upper-body pixels, requires visible motion for every outfit, and saves a contact sheet for visual inspection. macOS source and packaging syntax are checked on Windows; the macOS application itself must be checked on a Mac.
