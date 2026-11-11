"""Cerveau distant : API Claude (SDK officiel Anthropic). Nécessite une clé et Internet."""
from __future__ import annotations

import os

import anthropic

from ..config import Config
from . import MAX_TOURS_OUTILS, ErreurCerveau


class Claude:
    def __init__(self, cfg: Config):
        self.modele, self.effort = cfg.claude_modele, cfg.claude_effort
        self._client: anthropic.Anthropic | None = None

    def disponible(self) -> tuple[bool, str]:
        if os.environ.get("ANTHROPIC_API_KEY") or os.environ.get("ANTHROPIC_AUTH_TOKEN"):
            return True, "clé configurée"
        return False, "variable ANTHROPIC_API_KEY absente"

    def amorcer(self, journal: list[tuple[str, str]]) -> list:
        return [{"role": role, "content": texte} for role, texte in journal]

    def tour(self, natif: list, texte: str, lancer_outil, consigne: str, outils: list) -> str:
        if self._client is None:
            self._client = anthropic.Anthropic()
        definitions = [{"name": o.nom, "description": o.description, "input_schema": o.schema} for o in outils]
        natif.append({"role": "user", "content": texte})
        for _ in range(MAX_TOURS_OUTILS):
            try:
                r = self._client.beta.messages.create(
                    model=self.modele, max_tokens=16000, system=consigne, tools=definitions, messages=natif,
                    thinking={"type": "adaptive"}, output_config={"effort": self.effort},
                    cache_control={"type": "ephemeral"},
                    # si le modèle décline pour raison de politique, l'API rejoue sur un modèle de repli
                    betas=["server-side-fallback-2026-07-01"], fallbacks="default")
            except anthropic.AuthenticationError as e:
                raise ErreurCerveau("Clé API Claude refusée : vérifiez ANTHROPIC_API_KEY.") from e
            except anthropic.RateLimitError as e:
                raise ErreurCerveau("Limite de débit de l'API Claude atteinte ; réessayez dans un instant.") from e
            except anthropic.APIStatusError as e:
                raise ErreurCerveau(f"L'API Claude a répondu une erreur {e.status_code} : {e.message}") from e
            except anthropic.APIConnectionError as e:
                raise ErreurCerveau("API Claude injoignable : pas de connexion Internet ?") from e

            if r.stop_reason == "refusal":
                raise ErreurCerveau("Le modèle a décliné cette demande.")
            natif.append({"role": "assistant", "content": r.content})   # blocs complets, réflexion comprise
            ecrit = "".join(b.text for b in r.content if b.type == "text").strip()
            if r.stop_reason == "max_tokens":
                return ecrit or "La réponse a été coupée avant la fin."
            appels = [b for b in r.content if b.type == "tool_use"]
            if not appels:
                return ecrit
            resultats = []
            for b in appels:
                contenu, erreur = lancer_outil(b.name, b.input)
                resultats.append({"type": "tool_result", "tool_use_id": b.id, "content": contenu, "is_error": erreur})
            natif.append({"role": "user", "content": resultats})    # tous les résultats dans un seul message
        raise ErreurCerveau("Trop d'appels d'outils successifs ; reformulez la demande.")
