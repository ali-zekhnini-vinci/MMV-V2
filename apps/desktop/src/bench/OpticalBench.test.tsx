// @vitest-environment jsdom
// Parcours 04-A sur le BANC ISOLÉ (prototype, données fictives, droits simulés) : J-04-01, J-04-02, J-04-04, J-04-06,
// J-04-07, J-04-09 (clavier), simplification D-04-16, enregistrement simplifié D-04-17, états du Lot 3 et états
// complémentaires (J-04-08, pièces sources, reporté au backlog permanent par D-04-16). La garde réseau du banc est installée :
// aucune requête ne sort (preuve en fin de chaque test). Ce ne sont PAS des preuves serveur ni natives.
import assert from "node:assert/strict";
import { cleanup, render, screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, test } from "vitest";
import { type BenchNetwork, installBenchNetwork } from "./benchNetwork.ts";
import { createBenchClients } from "./benchSeed.ts";
import { OpticalBench } from "./OpticalBench.tsx";

let network: BenchNetwork | null = null;

afterEach(() => {
  cleanup();
  network?.restore();
  network = null;
});

async function openBench(route: string, profileId = "all", step?: number) {
  window.innerWidth = 1440;
  const clients = createBenchClients();
  network = installBenchNetwork(() => clients);
  const user = userEvent.setup();
  render(<OpticalBench clients={clients} network={network} initialRoute={route} profileId={profileId} step={step} />);
  await screen.findByRole("note", { name: "Banc de démonstration" });
  return { user, network };
}

const tab = (name: string) => screen.getByRole("tab", { name });

async function openRecord(user: ReturnType<typeof userEvent.setup>, reference: string) {
  await user.click(await screen.findByRole("button", { name: new RegExp(`^${reference}`) }));
  return screen.findByRole("heading", { level: 1, name: new RegExp(reference) });
}

/** Client concerné affiché dans la carte « Informations générales » du formulaire (maquette Lot 3, .clictx). */
function assertFormClient(name: string, reference: string) {
  const group = screen.getByRole("group", { name: "Client concerné" });
  within(group).getByText(name);
  within(group).getByText(new RegExp(reference));
}

function assertNoNetwork() {
  assert.deepEqual(network?.blocked, [], "aucun appel réseau ne doit être émis par le banc");
}

describe("banc 04-A — isolement", () => {
  test("bannière permanente « BANC DE DÉMONSTRATION », décisions PROPOSED, aucun appel réseau", async () => {
    await openBench("/");
    const banner = screen.getByRole("note", { name: "Banc de démonstration" });
    assert.match(banner.textContent ?? "", /BANC DE DÉMONSTRATION ET DE TEST 04-A — PROTOTYPE/);
    assert.match(banner.textContent ?? "", /PROPOSED/);
    assert.match(banner.textContent ?? "", /Ordonnances — phase ultérieure/);
    await screen.findByRole("heading", { name: "Banc 04-A — dossier optique (prototype)" });
    assertNoNetwork();
  });
});

describe("J-04-01 — depuis la fiche client", () => {
  test("onglet Dossier optique : deux sections distinctes vides, choix explicite du type, brouillon, retour à l'onglet", async () => {
    const { user } = await openBench("/clients/cli-fictif-3");
    await screen.findByRole("heading", { level: 1, name: "FICTIF-CHARLIE Noa" });
    assert.equal(screen.queryByRole("tab", { name: "Ordonnances" }), null);
    await user.click(tab("Dossier optique"));
    const prescriptions = await screen.findByRole("region", { name: /Ordonnances médicales/ });
    const measurements = screen.getByRole("region", { name: /Mesures magasin/ });
    within(prescriptions).getByText("Aucune ordonnance médicale n'est enregistrée pour ce client.");
    within(measurements).getByText("Aucune mesure magasin n'est enregistrée pour ce client.");

    await user.click(screen.getByRole("button", { name: "Ajouter…" }));
    const chooser = await screen.findByRole("alertdialog", { name: "Que souhaitez-vous ajouter ?" });
    const options = within(chooser).getAllByRole("button").filter((button) => /Ordonnance médicale|Mesure magasin/.test(button.textContent ?? ""));
    assert.equal(options.length, 2);
    assert.equal(document.activeElement, options[0], "focus initial sur la première option");
    await user.keyboard("{Escape}");
    await waitFor(() => assert.equal(screen.queryByRole("alertdialog"), null));

    await user.click(screen.getByRole("button", { name: "Ajouter…" }));
    await user.click(within(await screen.findByRole("alertdialog")).getByRole("button", { name: /^Ordonnance médicale/ }));
    await screen.findByRole("heading", { level: 1, name: "Nouvelle ordonnance médicale" });
    assertFormClient("FICTIF-CHARLIE Noa", "CLI-FICTIF-003");
    await user.type(screen.getByLabelText("Sphère OD"), "+1,25");
    await user.type(screen.getByLabelText("Axe OD"), "0");
    await user.click(screen.getByRole("button", { name: "Enregistrer comme brouillon" }));

    await screen.findByRole("heading", { level: 1, name: /Ordonnance médicale RX-FICTIF-/ });
    screen.getByText("Brouillon enregistré");
    screen.getByText("Brouillon non confirmé");
    const values = screen.getByRole("table", { name: /Correction optique/ });
    within(values).getByText("+1,25");
    within(values).getByText("0");
    await user.click(screen.getByRole("button", { name: /^Retour à la fiche — FICTIF-CHARLIE Noa/ }));
    await screen.findByRole("heading", { level: 1, name: "FICTIF-CHARLIE Noa" });
    assert.equal(tab("Dossier optique").getAttribute("aria-selected"), "true", "onglet rouvert sans nouvelle recherche");
    await screen.findByText("Brouillon");
    assertNoNetwork();
  });

  test("client archivé : consultation seule, « Ajouter… » désactivé ; saisie forcée refusée par le service (409)", async () => {
    const { user } = await openBench("/clients/cli-fictif-4");
    await screen.findByRole("heading", { level: 1, name: "FICTIF-DELTA Alix" });
    await user.click(tab("Dossier optique"));
    await screen.findByText("Client archivé — consultation seule");
    assert.equal((screen.getByRole("button", { name: "Ajouter…" }) as HTMLButtonElement).disabled, true);

    await user.click(screen.getByRole("button", { name: "Accueil du banc" }));
    await user.click(await screen.findByRole("button", { name: /^Saisie forcée pour FICTIF-DELTA Alix/ }));
    await screen.findByRole("heading", { level: 1, name: "Nouvelle ordonnance médicale" });
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));
    const alert = await screen.findByRole("alert");
    within(alert).getByText("Client archivé");
    assertNoNetwork();
  });
});

describe("J-04-02 — depuis la liste globale", () => {
  test("liste : type sur chaque ligne, invalidés exclus par défaut puis inclus, recherche, aucun résultat", async () => {
    const { user } = await openBench("/optique");
    const table = await screen.findByRole("table", { name: /Dossiers optiques, page 1 sur 2/ });
    assert.ok(within(table).getAllByText("Ordonnance médicale").length > 0);
    assert.ok(within(table).getAllByText("Mesure magasin").length > 0);
    screen.getByText(/10 enregistrements · tous types · invalidés exclus/);
    assert.equal(within(table).queryByText("Invalidé"), null);
    await user.click(screen.getByRole("button", { name: "Inclure les invalidés" }));
    await screen.findByText(/11 enregistrements · tous types · invalidés inclus/);

    await user.type(screen.getByRole("searchbox", { name: "Rechercher par client ou prescripteur" }), "Delta");
    await screen.findByText(/^1 enregistrement · tous types/);
    assert.match(within(screen.getByRole("table")).getAllByRole("row")[1].textContent ?? "", /FICTIF-DELTA Alix/);
    await user.clear(screen.getByRole("searchbox"));
    await user.type(screen.getByRole("searchbox"), "zzzz");
    await screen.findByRole("heading", { name: "Aucun résultat pour « zzzz »" });
    await user.click(screen.getByRole("button", { name: "Effacer la recherche" }));
    await screen.findByRole("table");
    assertNoNetwork();
  });

  test("« Nouveau… » → type → client ACTIF (ClientPicker réutilisé) → formulaire avec client conservé", async () => {
    const { user, network } = await openBench("/optique");
    await user.click(await screen.findByRole("button", { name: "Nouveau…" }));
    await user.click(within(await screen.findByRole("alertdialog")).getByRole("button", { name: /^Mesure magasin/ }));
    const search = await screen.findByRole("combobox", { name: "Rechercher un client actif" });
    await user.type(search, "Fictif");
    const listbox = await screen.findByRole("listbox", { name: "Clients actifs" });
    await within(listbox).findByText("FICTIF-ALPHA Camille");
    assert.equal(within(listbox).queryByText("FICTIF-DELTA Alix"), null, "client archivé exclu");
    await user.click(within(listbox).getByText("FICTIF-ALPHA Camille"));
    await screen.findByRole("heading", { level: 1, name: "Nouvelle mesure magasin" });
    assertFormClient("FICTIF-ALPHA Camille", "CLI-FICTIF-001");
    screen.getByLabelText("Collaborateur ayant mesuré");
    assert.equal(screen.queryByLabelText("Prescripteur — nom (tel que sur le document)"), null, "aucun prescripteur sur une mesure magasin");
    assert.ok(network.served.length > 0);
    assertNoNetwork();
  });
});

describe("grille OD / OG neutre (Q-04-158)", () => {
  test("saisie au clavier conservée telle quelle, aucun champ désactivé ni requis, absence ≠ 0", async () => {
    const { user } = await openBench("/clients/cli-fictif-3");
    await user.click(await screen.findByRole("tab", { name: "Dossier optique" }));
    await user.click(await screen.findByRole("button", { name: "Transcrire une ordonnance médicale" }));
    await screen.findByRole("heading", { level: 1, name: "Nouvelle ordonnance médicale" });
    const sphere = screen.getByLabelText("Sphère OD") as HTMLInputElement;
    await user.type(sphere, "-0.5");
    await user.tab();
    assert.equal(sphere.value, "-0.5", "aucune normalisation, aucun pas imposé à la saisie au clavier");
    const grid = [...document.querySelectorAll<HTMLElement>("fieldset.optical-eye")];
    assert.equal(grid.length, 2);
    for (const input of grid.flatMap((group) => within(group).getAllByRole("textbox"))) {
      assert.equal((input as HTMLInputElement).disabled, false);
      assert.equal(input.getAttribute("aria-required"), null);
      assert.equal((input as HTMLInputElement).required, false);
    }
    assert.equal((screen.getByLabelText("Cylindre OD") as HTMLInputElement).value, "", "champ vide = non renseigné, jamais 0");
    assertNoNetwork();
  });

  test("ordre de Tab : OD puis directement OG (aucun état de l'œil) ; erreurs de forme du service sous le champ", async () => {
    const { user } = await openBench("/clients/cli-fictif-3");
    await user.click(await screen.findByRole("tab", { name: "Dossier optique" }));
    await user.click(await screen.findByRole("button", { name: "Transcrire une ordonnance médicale" }));
    const acuityOd = await screen.findByLabelText("Acuité visuelle OD");
    acuityOd.focus();
    await user.tab();
    assert.equal(document.activeElement, screen.getByLabelText("Sphère OG"), "après l'acuité OD : la sphère OG");

    await user.type(screen.getByLabelText("Sphère OD"), "abc");
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));
    const message = await screen.findByText("Nombre attendu, tel qu'il figure sur la source (ex. +1,25).", { selector: ".clients-field__error" });
    assert.ok((screen.getByLabelText("Sphère OD").getAttribute("aria-describedby") ?? "").includes(message.id));
    assert.equal((screen.getByLabelText("Sphère OD") as HTMLInputElement).value, "abc", "saisie conservée");
    assert.equal(document.activeElement, screen.getByLabelText("Sphère OD"), "focus sur le premier champ en erreur");
    assertNoNetwork();
  });
});

// D-04-15 (APPROVED) : maquette Lot 3, contrôles rapides ± ; le pas reste un paramètre de démonstration (D-04-06).
describe("contrôles rapides ± (D-04-15, Q-04-170 à Q-04-176)", () => {
  type User = ReturnType<typeof userEvent.setup>;
  const value = (label: string) => (screen.getByLabelText(label) as HTMLInputElement).value;
  const press = async (user: User, name: string, times = 1) => {
    for (let index = 0; index < times; index += 1) await user.click(screen.getByRole("button", { name }));
  };
  async function openForm(chooser = "Transcrire une ordonnance médicale", step?: number) {
    const opened = await openBench("/clients/cli-fictif-3", "all", step);
    await opened.user.click(await screen.findByRole("tab", { name: "Dossier optique" }));
    await opened.user.click(await screen.findByRole("button", { name: chooser }));
    await screen.findByRole("heading", { level: 1, name: /^Nouvelle/ });
    return opened;
  }

  test("chaque champ ± de OD et OG : − à gauche, valeur, + à droite ; mise à jour immédiate et annoncée ; yeux indépendants", async () => {
    const { user } = await openForm();
    for (const side of ["OD", "OG"]) {
      for (const field of ["Sphère", "Cylindre", "Addition"]) {
        const input = screen.getByLabelText(`${field} ${side}`);
        const row = input.closest(".optical-stepper") as HTMLElement;
        const buttons = within(row).getAllByRole("button", { hidden: true }).filter((button) => /^(Diminuer|Augmenter)/.test(button.getAttribute("aria-label") ?? ""));
        assert.deepEqual(
          buttons.map((button) => button.getAttribute("aria-label")),
          [`Diminuer ${field} ${side} de 0,25`, `Augmenter ${field} ${side} de 0,25`],
          "− avant la valeur, + après",
        );
        assert.equal(buttons[0].textContent, "−0,25");
        assert.equal(buttons[1].textContent, "+0,25");
      }
    }
    await press(user, "Augmenter Sphère OD de 0,25", 3);
    assert.equal(value("Sphère OD"), "+0,75");
    await screen.findByText("Sphère OD : +0,75");
    assert.equal(value("Sphère OG"), "", "OG inchangé");
    await press(user, "Diminuer Cylindre OG de 0,25", 2);
    assert.equal(value("Cylindre OG"), "-0,50");
    assert.equal(value("Cylindre OD"), "", "OD inchangé");
    await press(user, "Augmenter Addition OD de 0,25", 7);
    assert.equal(value("Addition OD"), "+1,75");
    assert.equal(value("Addition OG"), "");
    // Prisme (pas non arrêté, mandat 04-B), Axe, Base et Acuité : aucun pas imposé (pas de boutons ±).
    for (const label of ["Prisme OD", "Prisme OG", "Axe OD", "Base OD", "Acuité visuelle OD"]) {
      const input = screen.getByLabelText(label);
      assert.equal(input.closest(".optical-stepper"), null, `${label} sans boutons ±`);
    }
    assertNoNetwork();
  });

  test("valeurs positives, négatives, nulles et absentes ; alternance clavier / clics sans arrondi ni alignement sur le pas", async () => {
    const { user } = await openForm();
    const sphere = screen.getByLabelText("Sphère OD") as HTMLInputElement;
    assert.equal(sphere.value, "", "absent : vide, jamais 0");
    assert.equal(sphere.placeholder, "—");
    await press(user, "Diminuer Sphère OD de 0,25");
    assert.equal(sphere.value, "-0,25", "clic depuis le vide = nouvelle saisie explicite");
    await press(user, "Augmenter Sphère OD de 0,25");
    assert.equal(sphere.value, "0,00", "zéro explicite, distinct de l'absence");

    await user.clear(sphere);
    await user.type(sphere, "-2,13");
    await user.tab();
    assert.equal(sphere.value, "-2,13", "valeur manuelle hors pas conservée");
    await press(user, "Augmenter Sphère OD de 0,25");
    assert.equal(sphere.value, "-1,88", "aucun arrondi au pas de démonstration");
    await user.type(sphere, "5");
    assert.equal(sphere.value, "-1,885", "la saisie au clavier reprend la main");
    await press(user, "Diminuer Sphère OD de 0,25");
    assert.equal(sphere.value, "-2,135");

    const cylinder = screen.getByLabelText("Cylindre OD") as HTMLInputElement;
    await user.type(cylinder, "+0,75");
    await press(user, "Diminuer Cylindre OD de 0,25", 4);
    assert.equal(cylinder.value, "-0,25", "aucune transposition : le signe suit le calcul, rien n'est converti");

    const axis = screen.getByLabelText("Axe OD") as HTMLInputElement;
    await user.type(axis, "0");
    await user.tab();
    assert.equal(axis.value, "0", "axe non normalisé");
    assertNoNetwork();
  });

  test("valeur non numérique : boutons ± inactifs, valeur jamais modifiée, aide sous le champ", async () => {
    const { user } = await openForm();
    const sphere = screen.getByLabelText("Sphère OD") as HTMLInputElement;
    await user.type(sphere, "abc");
    const plus = screen.getByRole("button", { name: "Augmenter Sphère OD de 0,25" });
    assert.equal(plus.getAttribute("aria-disabled"), "true");
    await user.click(plus);
    assert.equal(sphere.value, "abc");
    const hint = screen.getByText(/Valeur non numérique conservée telle quelle/);
    assert.ok((sphere.getAttribute("aria-describedby") ?? "").includes(hint.id));
    assertNoNetwork();
  });

  test("réinitialisation explicite d'un champ : « Effacer » remet le champ à non renseigné", async () => {
    const { user } = await openForm();
    await press(user, "Augmenter Addition OD de 0,25", 2);
    assert.equal(value("Addition OD"), "+0,50");
    await user.click(screen.getByRole("button", { name: "Effacer Addition OD" }));
    assert.equal(value("Addition OD"), "");
    assert.equal(screen.queryByRole("button", { name: "Effacer Addition OD" }), null);
    await user.type(screen.getByLabelText("Axe OG"), "90");
    await user.click(screen.getByRole("button", { name: "Effacer Axe OG" }));
    assert.equal(value("Axe OG"), "");
    assertNoNetwork();
  });

  test("clavier : Tab / Maj+Tab sur les seuls champs (boutons ± hors de l'ordre), ↑ / ↓ = ± pas, Alt+↓ ouvre la liste de bases proposées", async () => {
    const { user } = await openForm();
    const order = ["Sphère OD", "Cylindre OD", "Axe OD", "Addition OD", "Prisme OD", "Base OD", "Acuité visuelle OD"];
    screen.getByLabelText(order[0]).focus();
    for (const label of order.slice(1)) {
      await user.tab();
      assert.equal(document.activeElement, screen.getByLabelText(label), `Tab → ${label}`);
    }
    await user.tab();
    assert.equal(document.activeElement, screen.getByLabelText("Sphère OG"), "après OD : OG");
    for (const label of [...order].reverse()) {
      await user.tab({ shift: true });
      assert.equal(document.activeElement, screen.getByLabelText(label), `Maj+Tab → ${label}`);
    }

    const sphere = screen.getByLabelText("Sphère OD") as HTMLInputElement;
    sphere.focus();
    await user.keyboard("{ArrowUp}{ArrowUp}{ArrowDown}");
    assert.equal(sphere.value, "+0,25");
    assert.equal(document.activeElement, sphere, "le focus reste sur le champ");

    const base = screen.getByLabelText("Base OD") as HTMLInputElement;
    base.focus();
    await user.keyboard("{Alt>}{ArrowDown}{/Alt}");
    const menu = await screen.findByRole("menu");
    await user.click(within(menu).getByRole("menuitem", { name: "Nasal" }));
    assert.equal(base.value, "Nasal");
    await user.clear(base);
    await user.type(base, "BN (telle que sur la source)");
    assert.equal(base.value, "BN (telle que sur la source)", "saisie libre conservée");
    assertNoNetwork();
  });

  test("conservation : un clic ± rend le formulaire modifié (garde de sortie) ; valeurs enregistrées telles qu'affichées", async () => {
    const { user } = await openForm();
    await press(user, "Diminuer Sphère OD de 0,25", 9);
    await user.type(screen.getByLabelText("Sphère OG"), "-2,13");
    await user.click(screen.getByRole("button", { name: "Dossiers optiques" }));
    const dialog = await screen.findByRole("alertdialog", { name: "Quitter sans enregistrer ?" });
    await user.click(within(dialog).getByRole("button", { name: "Continuer la saisie" }));
    assert.equal(value("Sphère OD"), "-2,25");
    assert.equal(value("Sphère OG"), "-2,13");
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));
    await screen.findByRole("heading", { level: 1, name: /Ordonnance médicale RX-FICTIF-/ });
    screen.getByText("Ordonnance médicale enregistrée");
    const table = screen.getByRole("table", { name: /Correction optique/ });
    within(table).getByText("-2,25");
    within(table).getByText("-2,13");
    assertNoNetwork();
  });

  test("pas de démonstration paramétrable (banc) ; mesure magasin : mêmes contrôles, provenance propre, jamais une prescription", async () => {
    const { user } = await openForm("Saisir une mesure magasin", 0.125);
    await screen.findByRole("heading", { level: 1, name: "Nouvelle mesure magasin" });
    await press(user, "Augmenter Sphère OD de 0,125", 3);
    assert.equal(value("Sphère OD"), "+0,375");
    screen.getByText(/ce n.est pas une prescription délivrée par un médecin/);
    assert.equal(screen.queryByLabelText("Prescripteur — nom (tel que sur le document)"), null);
    screen.getByLabelText("Collaborateur ayant mesuré");
    assertFormClient("FICTIF-CHARLIE Noa", "CLI-FICTIF-003");
    assertNoNetwork();
  });
});

describe("J-04-04 — rectification, révision confirmée toujours affichée, comparaison", () => {
  test("modifier → brouillon ; la dernière révision confirmée reste affichée ; comparer ; confirmer → révision 2", async () => {
    const { user } = await openBench("/optique");
    await user.click(await screen.findByRole("button", { name: "Consulter Ordonnance médicale RX-FICTIF-0006" }));
    await screen.findByRole("heading", { level: 1, name: "Ordonnance médicale RX-FICTIF-0006" });
    screen.getByText("Préparer une vente · Phase ultérieure");
    assert.equal((screen.getByRole("button", { name: /Préparer une vente/ }) as HTMLButtonElement).disabled, true);
    await user.click(screen.getByRole("button", { name: "Modifier" }));
    const dialog = await screen.findByRole("alertdialog", { name: "Modifier cet enregistrement ?" });
    await user.click(within(dialog).getByRole("button", { name: "Modifier" }));
    within(dialog).getByText("Choisissez un motif de modification.");
    await user.selectOptions(within(dialog).getByLabelText("Motif"), "transcription_error");
    await user.click(within(dialog).getByRole("button", { name: "Modifier" }));

    await screen.findByRole("heading", { level: 1, name: "Modifier — ordonnance médicale" });
    screen.getByText(/La dernière révision confirmée \(révision 1\) reste affichée/);
    assert.equal(screen.queryByText(/en vigueur/), null, "jamais « en vigueur » (aucune validité suggérée)");
    const sphere = screen.getByLabelText("Sphère OD") as HTMLInputElement;
    assert.equal(sphere.value, "-0,75", "brouillon prérempli de la révision de base");
    await user.clear(sphere);
    await user.type(sphere, "-1,10");
    await user.click(screen.getByRole("button", { name: "Enregistrer comme brouillon" }));

    await screen.findByText(/^Modification en cours — ouverte le/);
    screen.getByRole("heading", { name: "Dernière révision confirmée — révision 1" });
    within(screen.getByRole("table", { name: /Correction optique — Révision 1/ })).getByText("-0,75");
    const compare = screen.getByRole("table", { name: /Comparaison/ });
    within(compare).getByText("OD — Sphère");
    within(compare).getByText("Différent");
    await user.click(screen.getByRole("button", { name: "Voir le brouillon" }));
    within(screen.getByRole("table", { name: /Correction optique — Brouillon de rectification/ })).getByText("-1,10");
    await user.click(screen.getByRole("button", { name: "Confirmer la modification" }));
    const confirm = await screen.findByRole("alertdialog", { name: "Confirmer la transcription ?" });
    within(confirm).getByText(/Cela ne signifie pas « validé médicalement »/);
    within(confirm).getByText(/OD : Sph -1,10/);
    await user.click(within(confirm).getByRole("button", { name: "Confirmer" }));
    await screen.findByText("Révision 2 enregistrée");
    screen.getByRole("heading", { name: "Dernière révision confirmée — révision 2" });
    assert.equal(screen.queryByText(/en vigueur/), null);
    assertNoNetwork();
  });

  test("brouillon confirmé depuis le détail : un seul œil suffit à la règle de démonstration (OG et provenance vides acceptés)", async () => {
    const { user } = await openBench("/");
    await openRecord(user, "RX-FICTIF-0004");
    await user.click(screen.getByRole("button", { name: "Confirmer la transcription" }));
    await user.click(within(await screen.findByRole("alertdialog")).getByRole("button", { name: "Confirmer" }));
    await screen.findByText("Révision 1 enregistrée");
    screen.getByRole("heading", { name: "Dernière révision confirmée — révision 1" });
    within(screen.getByRole("table", { name: /Correction optique/ })).getByText("0,00");
    assertNoNetwork();
  });
});

describe("J-04-06 — deux postes (GAP-04-09)", () => {
  test("écriture d'un autre poste : 409, saisie conservée, rechargement proposé", async () => {
    const { user } = await openBench("/");
    await openRecord(user, "RX-FICTIF-0004");
    await user.click(screen.getByRole("button", { name: "Modifier" }));
    await screen.findByRole("heading", { level: 1, name: /^Brouillon/ });
    await user.click(screen.getByRole("button", { name: "Autre poste : modifier l'enregistrement ouvert" }));
    const notes = screen.getByLabelText("Remarques complémentaires (facultatif)") as HTMLTextAreaElement;
    await user.type(notes, " poste A");
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));
    const alert = await screen.findByRole("alert");
    within(alert).getByText("Modifié depuis un autre poste");
    within(alert).getByText(/rien n'a été enregistré/);
    assert.equal(within(alert).queryByRole("button", { name: "Laisser en brouillon" }), null, "l'enregistrement lui-même a échoué");
    assert.match(notes.value, /poste A$/, "saisie conservée à l'écran");
    within(alert).getByRole("button", { name: "Recharger" });
    assertNoNetwork();
  });
});

describe("J-04-07 — invalidation (pas de suppression)", () => {
  test("menu secondaire « Invalider… », motif, bandeau ; brouillon de rectification abandonné dans la même opération", async () => {
    const { user } = await openBench("/");
    await openRecord(user, "RX-FICTIF-0002");
    assert.equal(screen.queryByRole("button", { name: /Supprimer/ }), null);
    await user.click(screen.getByRole("button", { name: "Plus d'actions" }));
    await user.click(await screen.findByRole("menuitem", { name: "Invalider…" }));
    const dialog = await screen.findByRole("alertdialog", { name: "Invalider cette ordonnance médicale ?" });
    within(dialog).getByText(/La rectification en cours sera abandonnée dans la même opération/);
    await user.selectOptions(within(dialog).getByLabelText("Motif"), "duplicate");
    await user.click(within(dialog).getByRole("button", { name: "Invalider" }));
    await screen.findByText(/^Invalidé le/);
    screen.getByText(/est conservé et signalé « Invalidé »/);
    screen.getByText(/La rectification ouverte a été abandonnée dans la même opération/);
    assert.equal(screen.queryByRole("button", { name: "Plus d'actions" }), null);
    assertNoNetwork();
  });
});

// D-04-16 (APPROVED) : ni « État de l'œil » ni pièces sources ; révisions, comparaison, motif et auteurs conservés.
describe("simplification du dossier optique (D-04-16)", () => {
  test("formulaire : aucun état de l'œil ni pièce ; chaque champ peut rester vide ; provenance de l'ordonnance conservée", async () => {
    const { user } = await openBench("/clients/cli-fictif-3");
    await user.click(await screen.findByRole("tab", { name: "Dossier optique" }));
    await user.click(await screen.findByRole("button", { name: "Transcrire une ordonnance médicale" }));
    await screen.findByRole("heading", { level: 1, name: "Nouvelle ordonnance médicale" });
    assert.equal(screen.queryByText(/État de l.œil/), null);
    assert.equal(screen.queryAllByRole("radio").length, 0, "aucun choix d'état par œil");
    assert.equal(screen.queryByRole("region", { name: /Pièces liées/ }), null);
    screen.getByLabelText("Prescripteur — nom (tel que sur le document)");
    screen.getByText("Date de prescription");
    await user.type(screen.getByLabelText("Sphère OD"), "+0,50");
    await user.click(screen.getByRole("button", { name: "Enregistrer comme brouillon" }));
    await screen.findByRole("heading", { level: 1, name: /Ordonnance médicale RX-FICTIF-/ });
    const values = screen.getByRole("table", { name: /Correction optique/ });
    assert.equal(within(values).queryByRole("columnheader", { name: "État" }), null);
    within(values).getByText("+0,50");
    assert.ok(within(values).getAllByLabelText("Non renseigné").length >= 13, "champs vides affichés « — », jamais 0");
    assertNoNetwork();
  });

  test("détail : aucune pièce source ; révisions, motif, auteurs et comparaison des valeurs conservés", async () => {
    const { user } = await openBench("/");
    await openRecord(user, "RX-FICTIF-0001");
    assert.equal(screen.queryByRole("region", { name: /Pièces source/ }), null);
    assert.equal(screen.queryByRole("button", { name: "Joindre une pièce" }), null);
    screen.getByText("Dr Fictif-Prescripteur A");
    screen.getByText("28/09/2026");
    const revisions = screen.getByRole("region", { name: /Historique des modifications/ });
    within(revisions).getByText(/motif : Complément d'information — Addition figurant au verso/);
    assert.ok(within(revisions).getAllByText(/confirmée le .* par Fictif-Opticien Un/).length === 2);
    const compare = within(revisions).getByRole("table", { name: /Comparaison/ });
    within(compare).getByText("OD — Addition");
    within(compare).getByText("OG — Addition");
    assert.equal(within(compare).queryByText(/— état$/), null);
    assert.equal(within(compare).queryByText("Pièces liées"), null);
    assertNoNetwork();
  });

  test("changement de profil : la première lecture utilise les droits du NOUVEAU profil (lecture seule)", async () => {
    const { user } = await openBench("/");
    await openRecord(user, "RX-FICTIF-0001");
    screen.getByRole("button", { name: "Modifier" });
    await user.selectOptions(screen.getByLabelText("Profil simulé (affichage seulement)"), "reader");
    await user.click(screen.getByRole("button", { name: "Accueil du banc" }));
    await openRecord(user, "RX-FICTIF-0001");
    assert.equal(screen.queryByRole("button", { name: "Modifier" }), null, "lecture seule déduite des droits");
    screen.getByText(/Consultation seule : ce profil ne permet pas de modifier/);
    assertNoNetwork();
  });
});

// D-04-17 (APPROVED) : « Enregistrer » principal, « Enregistrer comme brouillon » discret. Mandat 04-B : « Enregistrer »
// est ATOMIQUE (création ou modification + confirmation en une opération) — un refus n'enregistre rien. Règle minimale
// de démonstration du banc : aucune valeur optique → confirmation refusée (le serveur applique les critères approuvés).
describe("enregistrement simplifié (D-04-17)", () => {
  type User = ReturnType<typeof userEvent.setup>;
  const value = (label: string) => (screen.getByLabelText(label) as HTMLInputElement).value;
  const bannerOf = async (title: string) => {
    const banner = (await screen.findByText(title)).closest<HTMLElement>('[role="alert"]');
    assert.ok(banner, `bandeau « ${title} »`);
    return banner;
  };
  /** Référence attribuée au premier enregistrement, affichée dans le sous-titre du formulaire. */
  const formReference = () => /(?:RX|MES)-FICTIF-\d+/u.exec(document.querySelector(".optical-formhead .clients-head__sub")?.textContent ?? "")?.[0];
  async function newRecord(chooser = "Transcrire une ordonnance médicale") {
    const opened = await openBench("/clients/cli-fictif-3");
    await opened.user.click(await screen.findByRole("tab", { name: "Dossier optique" }));
    await opened.user.click(await screen.findByRole("button", { name: chooser }));
    await screen.findByRole("heading", { level: 1, name: /^Nouvelle/ });
    return opened;
  }
  async function openModification(user: User, reference: string) {
    await user.click(await screen.findByRole("button", { name: `Consulter Ordonnance médicale ${reference}` }));
    await screen.findByRole("heading", { level: 1, name: `Ordonnance médicale ${reference}` });
    await user.click(screen.getByRole("button", { name: "Modifier" }));
    const dialog = await screen.findByRole("alertdialog", { name: "Modifier cet enregistrement ?" });
    await user.selectOptions(within(dialog).getByLabelText("Motif"), "transcription_error");
    await user.click(within(dialog).getByRole("button", { name: "Modifier" }));
    await screen.findByRole("heading", { level: 1, name: "Modifier — ordonnance médicale" });
  }

  test("une action principale « Enregistrer », brouillon secondaire ; ± sur OD et OG puis Enregistrer → révision 1 confirmée sans étape intermédiaire", async () => {
    const { user } = await newRecord();
    const primary = screen.getByRole("button", { name: "Enregistrer" });
    assert.ok(primary.classList.contains("bg-accent"), "action principale bleue (variante primary)");
    assert.equal(primary.getAttribute("aria-keyshortcuts"), "Control+S");
    const secondary = screen.getByRole("button", { name: "Enregistrer comme brouillon" });
    assert.ok(!secondary.classList.contains("bg-accent"), "brouillon : importance visuelle inférieure");
    assert.equal(screen.queryByRole("button", { name: /Enregistrer et confirmer|Enregistrer le brouillon/ }), null);
    await user.click(screen.getByRole("button", { name: "Augmenter Sphère OD de 0,25" }));
    await user.click(screen.getByRole("button", { name: "Diminuer Sphère OG de 0,25" }));
    await user.click(primary);
    await screen.findByRole("heading", { level: 1, name: /Ordonnance médicale RX-FICTIF-/ });
    assert.equal(screen.queryByRole("alertdialog"), null, "aucun récapitulatif à valider");
    screen.getByText("Ordonnance médicale enregistrée");
    screen.getByText(/: révision 1 confirmée\.$/);
    screen.getByRole("heading", { name: "Dernière révision confirmée — révision 1" });
    const values = screen.getByRole("table", { name: /Correction optique/ });
    within(values).getByText("+0,25");
    within(values).getByText("-0,25");
    assertNoNetwork();
  });

  test("Ctrl+S = « Enregistrer » (confirmation), jamais le brouillon ; mesure magasin, provenance propre conservée", async () => {
    const { user } = await newRecord("Saisir une mesure magasin");
    await user.type(screen.getByLabelText("Sphère OD"), "-1,00");
    await user.keyboard("{Control>}s{/Control}");
    await screen.findByRole("heading", { level: 1, name: /Mesure magasin MES-FICTIF-/ });
    screen.getByText("Mesure magasin enregistrée");
    screen.getByRole("heading", { name: "Dernière révision confirmée — révision 1" });
    screen.getByText("Collaborateur ayant mesuré");
    assert.equal(screen.queryByText("Prescripteur"), null, "jamais une prescription (D-04-01)");
    assertNoNetwork();
  });

  test("confirmation refusée (422, aucune valeur optique) : rien n'est enregistré, saisie conservée, reprise sans doublon ; 0 est une valeur", async () => {
    const { user } = await newRecord();
    await user.type(screen.getByLabelText("Prescripteur — nom (tel que sur le document)"), "Dr Fictif-Test");
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));
    const banner = await bannerOf("Confirmation refusée — rien n'a été enregistré");
    within(banner).getByText("Correction optique");
    assert.equal(within(banner).queryByRole("button", { name: "Laisser en brouillon" }), null, "aucun brouillon orphelin à conserver");
    screen.getByRole("heading", { level: 1, name: "Nouvelle ordonnance médicale" });
    assert.equal(value("Prescripteur — nom (tel que sur le document)"), "Dr Fictif-Test", "saisie conservée");
    assert.equal(formReference(), undefined, "aucun enregistrement créé");

    await user.type(screen.getByLabelText("Sphère OD"), "0");
    await user.type(screen.getByLabelText("Addition OD"), "+2,00");
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));
    await screen.findByRole("heading", { level: 1, name: /Ordonnance médicale RX-FICTIF-/ });
    screen.getByText("Ordonnance médicale enregistrée");
    screen.getByRole("heading", { name: "Dernière révision confirmée — révision 1" });
    within(screen.getByRole("table", { name: /Correction optique/ })).getByText("0");
    await user.click(screen.getByRole("button", { name: "Dossiers optiques" }));
    await screen.findByText(/^11 enregistrements · tous types · invalidés exclus/);
    assertNoNetwork();
  });

  test("issue incertaine à l'enregistrement : rien n'est annoncé comme enregistré, nouvelle tentative sans doublon", async () => {
    const { user } = await newRecord();
    await user.type(screen.getByLabelText("Sphère OD"), "+1,00");
    await user.click(screen.getByRole("button", { name: "Échec de la prochaine confirmation" }));
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));
    const banner = await bannerOf("Enregistrement non confirmé");
    within(banner).getByText(/sans créer de doublon/);
    assert.equal(within(banner).queryByRole("button", { name: "Laisser en brouillon" }), null);
    assert.equal(value("Sphère OD"), "+1,00", "saisie conservée");
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));
    await screen.findByRole("heading", { level: 1, name: /Ordonnance médicale RX-FICTIF-/ });
    screen.getByText("Ordonnance médicale enregistrée");
    await user.click(screen.getByRole("button", { name: "Dossiers optiques" }));
    await screen.findByText(/^11 enregistrements · tous types · invalidés exclus/);
    assertNoNetwork();
  });

  test("modification d'un enregistrement confirmé, conflit 409 à l'enregistrement : rien n'est écrit, révision confirmée préservée, reprise sans doublon", async () => {
    const { user } = await openBench("/optique");
    await openModification(user, "RX-FICTIF-0006");
    const sphere = screen.getByLabelText("Sphère OD");
    await user.clear(sphere);
    await user.type(sphere, "-1,00");
    await user.click(screen.getByRole("button", { name: "Conflit à la prochaine confirmation" }));
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));
    const banner = await bannerOf("Modifié depuis un autre poste");
    within(banner).getByText(/rien n'a été enregistré/);
    within(banner).getByRole("button", { name: "Recharger" });
    assert.equal(value("Sphère OD"), "-1,00", "saisie conservée à l'écran");

    // Rien n'a été enregistré : quitter demande confirmation (avertissement légitime), la révision 1 reste la référence.
    await user.click(screen.getByRole("button", { name: "Annuler" }));
    const leave = await screen.findByRole("alertdialog", { name: "Quitter sans enregistrer ?" });
    await user.click(within(leave).getByRole("button", { name: "Quitter sans enregistrer" }));
    await screen.findByRole("heading", { level: 1, name: "Ordonnance médicale RX-FICTIF-0006" });
    screen.getByRole("heading", { name: "Dernière révision confirmée — révision 1" });
    within(screen.getByRole("table", { name: /Correction optique — Révision 1/ })).getByText("-0,75");
    screen.getByText(/^Modification en cours — ouverte le/);

    await user.click(screen.getByRole("button", { name: "Modifier" }));
    await screen.findByRole("heading", { level: 1, name: "Modifier — ordonnance médicale" });
    assert.equal(value("Sphère OD"), "-0,75", "brouillon inchangé repris (aucune écriture partielle)");
    await user.clear(screen.getByLabelText("Sphère OD"));
    await user.type(screen.getByLabelText("Sphère OD"), "-1,00");
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));
    await screen.findByText("Ordonnance médicale enregistrée");
    screen.getByRole("heading", { name: "Dernière révision confirmée — révision 2" });
    const history = screen.getByRole("region", { name: /Historique des modifications/ });
    assert.ok(within(history).getAllByText(/^Révision 2$/).length >= 1);
    assert.equal(within(history).queryAllByText(/Brouillon de rectification/).length, 0, "aucun brouillon en double");
    assertNoNetwork();
  });

  test("double clic et Ctrl+S répété pendant l'enregistrement (latence simulée) : un seul enregistrement créé", async () => {
    const { user } = await newRecord();
    await user.selectOptions(screen.getByLabelText("Latence simulée"), "800");
    await user.type(screen.getByLabelText("Sphère OD"), "+0,75");
    await user.dblClick(screen.getByRole("button", { name: "Enregistrer" }));
    await user.keyboard("{Control>}s{/Control}");
    assert.equal((screen.getByRole("button", { name: "Enregistrement…" }) as HTMLButtonElement).disabled, true);
    assert.equal((screen.getByRole("button", { name: "Enregistrer comme brouillon" }) as HTMLButtonElement).disabled, true);
    await screen.findByText("Ordonnance médicale enregistrée", undefined, { timeout: 4000 });
    await user.selectOptions(screen.getByLabelText("Latence simulée"), "0");
    await user.click(screen.getByRole("button", { name: "Dossiers optiques" }));
    await screen.findByText(/^11 enregistrements · tous types · invalidés exclus/);
    assertNoNetwork();
  });

  test("brouillon incomplet enregistré au clavier, repris puis confirmé par « Enregistrer »", async () => {
    const { user } = await newRecord();
    screen.getByRole("button", { name: "Enregistrer comme brouillon" }).focus();
    await user.keyboard("{Enter}");
    await screen.findByRole("heading", { level: 1, name: /Ordonnance médicale RX-FICTIF-/ });
    screen.getByText("Brouillon enregistré");
    screen.getByText("Brouillon non confirmé");
    await user.click(screen.getByRole("button", { name: "Modifier" }));
    await screen.findByRole("heading", { level: 1, name: "Brouillon — ordonnance médicale" });
    await user.click(screen.getByRole("button", { name: "Augmenter Sphère OG de 0,25" }));
    await user.click(screen.getByRole("button", { name: "Enregistrer" }));
    await screen.findByText("Ordonnance médicale enregistrée");
    screen.getByRole("heading", { name: "Dernière révision confirmée — révision 1" });
    assertNoNetwork();
  });
});

describe("erreurs techniques bornées (états 9, 31)", () => {
  test("liste : erreur sans trace technique + Réessayer ; invalidation en échec : rien n'est invalidé", async () => {
    const { user } = await openBench("/");
    await user.click(screen.getByRole("button", { name: "Échec de la prochaine liste" }));
    await user.click(screen.getByRole("button", { name: "Dossiers optiques" }));
    // Échec réseau réessayé une fois après 1 s (shouldRetry) : l'attente par défaut de 1 s rendait le test aléatoire (O-04-27).
    await screen.findByRole("heading", { name: "Impossible de charger les dossiers optiques" }, { timeout: 4000 });
    await user.click(screen.getByRole("button", { name: "Réessayer" }));
    await screen.findByRole("table", { name: /Dossiers optiques/ });

    await user.click(screen.getByRole("button", { name: "Consulter Ordonnance médicale RX-FICTIF-0006" }));
    await screen.findByRole("heading", { level: 1, name: "Ordonnance médicale RX-FICTIF-0006" });
    await user.click(screen.getByRole("button", { name: "Échec de la prochaine écriture" }));
    await user.click(screen.getByRole("button", { name: "Plus d'actions" }));
    await user.click(await screen.findByRole("menuitem", { name: "Invalider…" }));
    const dialog = await screen.findByRole("alertdialog");
    await user.selectOptions(within(dialog).getByLabelText("Motif"), "entry_error");
    await user.click(within(dialog).getByRole("button", { name: "Invalider" }));
    const alert = await screen.findByRole("alert");
    within(alert).getByText("Erreur technique");
    within(alert).getByText(/Aucune modification n'a été enregistrée/);
    assert.equal(screen.queryByText(/^Invalidé le/), null);
    assertNoNetwork();
  });
});

describe("accès refusé et comptes sans droit optique (GAP-04-05, O-04-16)", () => {
  test("sans optical.read : fiche avec l'onglet reporté actuel, accès direct au module refusé", async () => {
    const { user } = await openBench("/clients/cli-fictif-1", "none");
    await screen.findByRole("heading", { level: 1, name: "FICTIF-ALPHA Camille" });
    assert.equal(screen.queryByRole("tab", { name: "Dossier optique" }), null);
    await user.click(tab("Ordonnances"));
    await screen.findByText("Ordonnances — phase ultérieure");
    await user.click(screen.getByRole("button", { name: "Dossiers optiques" }));
    await screen.findByRole("heading", { name: "Accès non autorisé" });
    assertNoNetwork();
  });

  test("garde de sortie : quitter un formulaire modifié demande confirmation", async () => {
    const { user } = await openBench("/clients/cli-fictif-3");
    await user.click(await screen.findByRole("tab", { name: "Dossier optique" }));
    await user.click(await screen.findByRole("button", { name: "Saisir une mesure magasin" }));
    await screen.findByRole("heading", { level: 1, name: "Nouvelle mesure magasin" });
    await user.type(screen.getByLabelText("Sphère OD"), "+0,50");
    await user.click(screen.getByRole("button", { name: "Dossiers optiques" }));
    const dialog = await screen.findByRole("alertdialog", { name: "Quitter sans enregistrer ?" });
    await user.click(within(dialog).getByRole("button", { name: "Continuer la saisie" }));
    assert.equal((screen.getByLabelText("Sphère OD") as HTMLInputElement).value, "+0,50");
    assertNoNetwork();
  });
});
