<p align="center">
  <img src="docs/banner.png" alt="DiscordNodeStereo — mic estéreo a 512 kbps no Discord, sempre." width="100%">
</p>

<p align="center">
  <a href="https://github.com/Dimasaas/DiscordNodeStereo/releases/latest"><img src="https://img.shields.io/github/v/release/Dimasaas/DiscordNodeStereo?label=vers%C3%A3o&color=7C3AED" alt="Versão"></a>
  <a href="https://github.com/Dimasaas/DiscordNodeStereo/releases"><img src="https://img.shields.io/github/downloads/Dimasaas/DiscordNodeStereo/total?label=downloads&color=06B6D4" alt="Downloads"></a>
  <img src="https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?logo=windows&logoColor=white" alt="Windows 10 | 11">
  <img src="https://img.shields.io/badge/C%23-WinForms-512BD4?logo=dotnet&logoColor=white" alt="C# WinForms">
  <img src="https://img.shields.io/badge/depend%C3%AAncias-zero-22C55E" alt="Zero dependências">
  <a href="LICENSE"><img src="https://img.shields.io/badge/licen%C3%A7a-MIT-6D28D9" alt="Licença MIT"></a>
</p>

<p align="center">
  Toda atualização do Discord baixa um módulo de voz novo e o seu <b>mic estéreo a 512 kbps</b> some.<br>
  O <b>DiscordNodeStereo</b> percebe sozinho e coloca o patch de volta: no Discord, no PTB e no Canary.
</p>

<p align="center">
  <a href="https://github.com/Dimasaas/DiscordNodeStereo/releases/latest/download/DiscordNodeStereo.exe">
    <img src="https://img.shields.io/badge/%E2%AC%87%EF%B8%8F%20%20Baixar-DiscordNodeStereo.exe-7C3AED?style=for-the-badge" alt="Baixar DiscordNodeStereo.exe">
  </a>
</p>

<p align="center">
  <img src="docs/mockup.png" alt="Janela do DiscordNodeStereo e o aviso de reinício" width="100%">
</p>

## ✨ Destaques

<table>
  <tr>
    <td width="50%" valign="top">
      <h3>🎙️ Sempre 512 kbps em estéreo</h3>
      O patch são dois arquivos que só funcionam juntos: <code>discord_voice.node</code> + <code>index.js</code>. Os dois vão embutidos no <code>.exe</code> e são instalados sempre em par.
    </td>
    <td width="50%" valign="top">
      <h3>🔎 Acha a versão mais nova</h3>
      Entra na maior pasta <code>app-*</code> e no maior <code>discord_voice-*</code>, comparando número a número (<code>1.0.10000</code> &gt; <code>1.0.9999</code>).
    </td>
  </tr>
  <tr>
    <td valign="top">
      <h3>🎧 Discord, PTB e Canary</h3>
      Verifica todos os que estiverem instalados, de uma vez, sem você escolher nada.
    </td>
    <td valign="top">
      <h3>👤 Funciona pra qualquer usuário</h3>
      Usa a pasta de quem está logado: <code>C:\Users\&lt;seu-usuário&gt;\AppData\Local</code>.
    </td>
  </tr>
  <tr>
    <td valign="top">
      <h3>🔁 Troca com o Discord aberto</h3>
      O Windows trava o <code>.node</code> em uso, mas deixa renomear: o antigo sai do caminho e o novo entra. Se algo falhar no meio, ele desfaz tudo (nada de meio patch). Backup dos originais em <code>.original</code>.
    </td>
    <td valign="top">
      <h3>🔔 Aviso na hora + Desfazer</h3>
      Depois de trocar, pergunta: <b>Fechar Discord</b> (taskkill), <b>Abrir Discord</b> (reinicia) ou <b>Depois</b>. Mudou de ideia? O botão <b>Desfazer</b> volta os arquivos originais.
    </td>
  </tr>
  <tr>
    <td valign="top">
      <h3>⏱️ No seu ritmo</h3>
      Verifica de 5 em 5 minutos até 1 vez por dia (padrão: a cada 1 hora). Também tem o botão <b>Verificar agora</b>.
    </td>
    <td valign="top">
      <h3>🪶 Leve e portátil</h3>
      Um <code>.exe</code> só, sem instalar nada. Liga com o Windows escondido na bandeja (dá pra ligar/desligar na janela ou no menu da bandeja), com 0% de CPU parado e ~7 MB de memória em uso.
    </td>
  </tr>
</table>

## 🚀 Como usar

1. **Baixe** o [`DiscordNodeStereo.exe`](https://github.com/Dimasaas/DiscordNodeStereo/releases/latest/download/DiscordNodeStereo.exe) e coloque onde quiser (Área de Trabalho, pendrive…).
2. **Abra.** Ele verifica na hora e troca o módulo de voz onde precisar.
3. **Clique em "Abrir Discord"** no aviso. O Discord reinicia com o mic estéreo a 512 kbps. 🎙️

Daí pra frente ele fica na bandeja e cuida sozinho de cada atualização do Discord.

> [!TIP]
> O Windows 11 esconde ícones novos da bandeja. Clique na setinha **^** perto do relógio e arraste o ícone do microfone para a barra.

## ⚙️ Como funciona

```mermaid
flowchart LR
    A["⏱️ Timer<br/>(a cada 1 h)"] --> B{"Para cada Discord<br/>normal · PTB · Canary"}
    B --> C["Maior pasta app-*"]
    C --> D["Maior discord_voice-*"]
    D --> E{"discord_voice.node + index.js<br/>já são os de 512 kbps?"}
    E -- sim --> F["✅ Tudo certo"]
    E -- não --> G["💾 Backup .original"]
    G --> H["🔁 Troca os dois juntos<br/>(ou desfaz se falhar)"]
    H --> I["🔔 Aviso: reinicie o Discord"]
```

Onde o arquivo mora:

```text
C:\Users\<usuário>\AppData\Local\Discord          (ou DiscordPTB / DiscordCanary)
└── app-1.0.9260                                  ← maior versão
    └── modules
        └── discord_voice-1                       ← maior módulo
            └── discord_voice
                ├── discord_voice.node            ← patch de 512 kbps estéreo
                ├── index.js                      ← patch (par do .node)
                ├── discord_voice.node.original   ← backup do Discord
                └── index.js.original             ← backup do Discord
```

> [!IMPORTANT]
> Os dois arquivos andam juntos. O `index.js` novo do Discord chama funções que o `.node` de 512 kbps não tem; com meio patch o Discord marca "instalação corrompida", cai num motor de voz reserva e fica lento. Por isso o DiscordNodeStereo nunca troca um sem o outro.

Para comparar, ele olha primeiro o tamanho e depois o **SHA-256** de cada arquivo. O patch embutido já vem com tamanho e hash calculados no build, então a verificação de hora em hora nem precisa descompactar nada.

## 🧰 Configurações

Tudo se configura pela janela. Os ajustes ficam no `DiscordNodeStereo.ini`, ao lado do `.exe`; se a pasta não deixar gravar, vão para `%APPDATA%\DiscordNodeStereo`.

| Chave | Padrão | Para que serve |
|---|---|---|
| `interval_minutes` | `60` | Intervalo entre verificações (qualquer valor a partir de 1). |
| `autostart` | `1` | Iniciar com o Windows (`HKCU\...\Run`, sem precisar de admin). |
| `tray_hint_shown` | `0` | Se o balão "continuo rodando na bandeja" já apareceu. |
| `paused` | `0` | `1` depois do **Desfazer**: não reinstala o patch até você clicar em **Reinstalar patch**. |
| `last_version.<Discord>` | — | Última versão vista de cada Discord, para avisar quando sair uma nova. |
| `source` | *(vazio)* | Só para testes: pasta com outro `discord_voice.node` + `index.js`. O normal é sempre o patch de 512 kbps embutido. |

## 🛠️ Compilando

Não precisa instalar nada: o `build.ps1` usa o compilador C# que já vem com o .NET Framework 4 do Windows.

```powershell
# o patch fica fora do repositório: ..\Standard\Standard.zip (discord_voice.node + index.js soltos)
.\build.ps1 -Test          # roda os testes e gera dist\DiscordNodeStereo.exe

# ou apontando para outro zip/pasta com os dois arquivos
.\build.ps1 -Package "D:\arquivos\patch-512kbps"
```

## 🧪 Testes

```text
Testes:
  ok    versões comparadas como número
  ok    pega a pasta app-* de maior número
  ok    troca .node e index.js juntos, com backup dos dois
  ok    não mexe quando os dois já estão certos
  ok    só o index.js errado: troca o index.js
  ok    nova versão usa o módulo mais novo
  ok    troca mesmo com o .node travado
  ok    se uma troca falha, desfaz a outra (nada de meio patch)
  ok    fonte gzip embutida tem o hash certo
  ok    erros: sem Discord, sem módulo, sem arquivo do patch
  ok    verifica Discord, PTB e Canary de uma vez
  ok    desfazer volta o .node e o index.js originais
  ok    desfazer sem patch instalado não mexe em nada

Todos os testes passaram.
```

O teste do "arquivo travado" carrega uma DLL de verdade para simular o Discord aberto e confere que a troca funciona mesmo assim.

## 📁 Estrutura

```text
├── src/
│   ├── Program.cs         entrada, instância única e .node embutido
│   ├── Core.cs            acha as versões e troca o arquivo (sem interface)
│   ├── MainForm.cs        janela, bandeja e timer
│   ├── RestartDialog.cs   aviso "Fechar / Abrir Discord"
│   ├── Settings.cs        .ini, log e início com o Windows
│   ├── Ui.cs              tema, ícone e painéis
│   └── app.manifest       DPI e permissões (roda sem admin)
├── tests/CoreTests.cs     testes do núcleo
├── assets/icon.ico
├── docs/                  imagens deste README
└── build.ps1
```

## ❓ Problemas comuns

<details>
<summary><b>Não acho o ícone na bandeja</b></summary>

O Windows 11 esconde ícones novos. Clique na setinha **^** perto do relógio, ou vá em *Configurações → Personalização → Barra de tarefas → Outros ícones da bandeja* e ative o DiscordNodeStereo. Abrir o `.exe` de novo também traz a janela de volta.
</details>

<details>
<summary><b>Discord lento / "Potentially corrupt installation"</b></summary>

Isso acontece com meio patch: só o `.node` trocado e o `index.js` do Discord. Era o bug da v1.0.0. Use a versão mais nova: ela instala os dois arquivos juntos.
</details>

<details>
<summary><b>"O Windows protegeu o computador" (SmartScreen)</b></summary>

O `.exe` não é assinado digitalmente. Clique em **Mais informações → Executar assim mesmo**.
</details>

<details>
<summary><b>"Módulo de voz ainda não baixado"</b></summary>

O Discord só baixa o módulo de voz na primeira vez que você entra num canal de voz. Entre em um e clique em **Verificar agora**.
</details>

<details>
<summary><b>Quero voltar ao arquivo original</b></summary>

Clique em **Desfazer** na janela. Ele volta o `discord_voice.node` e o `index.js` originais em todos os Discords e fica pausado até você clicar em **Reinstalar patch**. Depois é só reiniciar o Discord (o aviso já oferece o botão).

Na mão: feche o DiscordNodeStereo e o Discord, e na pasta `discord_voice` tire o `.original` do nome dos backups (`discord_voice.node.original` → `discord_voice.node`, `index.js.original` → `index.js`).
</details>

<details>
<summary><b>Como desinstalar</b></summary>

Desmarque **Iniciar com o Windows** (na janela ou no menu da bandeja), clique em **Sair** na bandeja e apague o `DiscordNodeStereo.exe`, o `DiscordNodeStereo.ini` e o `DiscordNodeStereo.log`.
</details>

## ⚠️ Aviso

O DiscordNodeStereo não tem relação com o Discord. Mexer nos arquivos do cliente pode ir contra os Termos de Serviço do Discord: use por sua conta e risco.

---

<p align="center">
  <img src="docs/logo.png" width="72" alt=""><br>
  <b>DiscordNodeStereo</b><br>
  Feito com 💜 por <a href="https://github.com/Dimasaas">@Dimasaas</a> · <a href="LICENSE">Licença MIT</a>
</p>
