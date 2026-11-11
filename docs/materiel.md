# Matériel pour le mode embarqué

```
            ┌──────────────────────────────┐
  micro USB │                              │ jack / USB
 ──────────►│   Raspberry Pi 5 (8 Go)      ├──────────► haut-parleur amplifié
            │   Sentinelle + Vosk + Piper  │
            │                              │ GPIO 17 ──► module relais ──► charge
            └──────┬───────────────┬───────┘
                   │ Wi-Fi/Ethernet│
          ┌────────▼──────┐  ┌─────▼──────────────┐
          │ Broker MQTT   │  │ PC avec GPU :      │
          │ ESP32, HA     │  │ Ollama (optionnel) │
          └───────────────┘  └────────────────────┘
```

| Repère | Élément | Remarque |
|---|---|---|
| 1 | Raspberry Pi 5, 8 Go | Un Pi 4 suffit si le cerveau est sur une autre machine ou chez Claude |
| 2 | Refroidissement actif | Indispensable si un modèle local tourne sur le Pi |
| 3 | Micro USB omnidirectionnel ou carte à deux micros | Le plus loin possible du haut-parleur |
| 4 | Haut-parleur amplifié 3 à 5 W | Sortie jack d'une carte son USB, ou ampli I2S |
| 5 | Module relais 3,3 V opto-isolé | Pour les appareils de type `gpio` |
| 6 | Carte microSD A2 32 Go ou SSD NVMe | Le SSD évite l'usure si l'on stocke les modèles |

Un modèle de langage local répond lentement sur un Pi seul : compter plusieurs secondes par
réponse avec un petit modèle. Pour une conversation fluide, faire pointer `[cerveau.ollama] url`
vers un PC du réseau équipé d'un GPU, ou basculer sur Claude.

Tout ce qui est câblé côté secteur derrière un relais relève des règles habituelles :
boîtier fermé, section de fil adaptée, protection en amont.
