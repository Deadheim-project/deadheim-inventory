# Deadheim Inventory UI

Plugin cliente para Valheim 1.0.21. Integra abas ao painel de criação do inventário que já abre com Tab.

## O que inclui

- Painel de equipamento somente para leitura, com os itens equipados e seus detalhes.
- Aba de atalhos que exibe a primeira linha do inventário e as teclas nativas 1–8.
- Aba de aparência com prévia local e integração opcional com `Deadheim.TransmogApi`.
- Aba Artesanato que devolve acesso à tela nativa de criação.
- Escala local ajustável em `BepInEx/config/Deadheim.InventoryUI.cfg`.

## O que não altera

- Tamanho, conteúdo, posições ou capacidade do inventário.
- Itens equipados, atalhos ou salvamento do personagem ao abrir, navegar ou fechar a interface.
- Regras do servidor para VIP, Alfaiate ou transmog. A troca visual é validada pelo mod principal.

Não inclui linhas extras, espaços de equipamento adicionais, loadouts, favoritos, painel de estatísticas nem dependência do AzuEPI.

## Instalação

Instale `DeadheimInventoryUI.dll` em `BepInEx/plugins` no cliente. O mod principal Deadheim deve estar instalado para usar a aba de aparência; sem ele, as demais abas continuam disponíveis. Não é necessário instalar este plugin no servidor dedicado.

Se `AzuExtendedPlayerInventory` estiver presente, o plugin não ativa a própria UI para evitar disputar o mesmo painel.

## Compilar

```powershell
dotnet build DeadheimInventoryUI.csproj -c Release
```

Por padrão compila contra `D:\valheim-ref`; outra árvore pode ser indicada com `VALHEIM_PATH`.
