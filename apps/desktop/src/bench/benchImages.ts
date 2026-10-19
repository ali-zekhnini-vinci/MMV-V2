// BANC 04-A — aperçu SYNTHÉTIQUE d'une pièce image, dessiné localement (canvas) au moment de l'affichage : aucune
// image réelle, aucun fichier, aucun réseau. Servi en `data:` (CSP actuelle `img-src 'self' data:`, aucun `blob:`).
// Indisponible hors navigateur (tests jsdom) : `null`, l'écran l'annonce.
export function renderSyntheticImage({ label, mediaType }: { label: string; mediaType: string }): string | null {
  if (typeof document === "undefined" || /jsdom/i.test(navigator.userAgent)) return null;
  const canvas = document.createElement("canvas");
  canvas.width = 640;
  canvas.height = 420;
  const context = canvas.getContext("2d");
  if (!context) return null;
  context.fillStyle = "#ffffff";
  context.fillRect(0, 0, canvas.width, canvas.height);
  context.strokeStyle = "#cdd4de";
  context.lineWidth = 2;
  context.strokeRect(12, 12, canvas.width - 24, canvas.height - 24);
  // Hachures : l'image ne peut pas être prise pour un document réel.
  context.strokeStyle = "#eef1f5";
  for (let x = -canvas.height; x < canvas.width; x += 28) {
    context.beginPath();
    context.moveTo(x, canvas.height);
    context.lineTo(x + canvas.height, 0);
    context.stroke();
  }
  context.fillStyle = "#0b1d3a";
  context.font = "bold 26px sans-serif";
  context.fillText("PIÈCE SYNTHÉTIQUE — BANC 04-A", 40, 80);
  context.font = "20px sans-serif";
  context.fillText(label, 40, 130);
  context.fillText(`Type annoncé : ${mediaType}`, 40, 165);
  context.fillStyle = "#c01427";
  context.font = "bold 20px sans-serif";
  context.fillText("Aucune donnée réelle · aucune valeur optique", 40, 360);
  return canvas.toDataURL("image/png");
}
