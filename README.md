# Landforms Need Curves

A mod for Vintage Story that allows altering world gen landform height generation (Y key positions) according to a
cubic bezier easing curve. Separate curves are used for above and below sea level, and overrides are possible for
single landforms.

Helpful utilities to help visualize the changes:

* https://cubic-bezier.com/
* http://tyron.at/vs/landformcreator.html

Auto Config Lib integration is supported, and changes can be made live for testing purposes combined with chunk
pruning. 1.20.7 appears to have a bug with pruning multiple times without restarting the world which limits the
usefulness of this.