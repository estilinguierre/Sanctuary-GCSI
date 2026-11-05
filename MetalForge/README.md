# MetalForge

Complément SolidWorks pour la métallerie-serrurerie : escaliers, garde-corps, portails, clôtures, menuiseries acier, platines, développés, débits et mises en barres.

- Contexte, stack et règles de travail : [`CLAUDE.md`](CLAUDE.md)
- Spécification et lots : [`docs/02_SPEC_METALFORGE.md`](docs/02_SPEC_METALFORGE.md)
- Inventaire des fonctions `F-XXX-nn` : [`docs/01_FONCTIONNALITES_METALCAD.md`](docs/01_FONCTIONNALITES_METALCAD.md)
- Guides par lot : [`docs/lots/`](docs/lots/)

```powershell
dotnet test src/MetalForge.Tests
msbuild MetalForge.sln /p:Configuration=Debug /p:Platform=x64
```

L'ancien configurateur (web + add-in WebView2) est conservé dans [`_ancien/`](_ancien/) ; ses calculs sont repris lot par lot.
