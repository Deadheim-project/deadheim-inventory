# Deadheim Inventory UI

Plugin cliente para Valheim 1.0.21. Integra abas ao painel de criação do inventário que já abre com Tab.

## O que inclui

- Painel único, sem abas, à esquerda do painel de crafting (que permanece nativo).
- Coluna esquerda: slots de equipamento (cabeça, peito, pernas, capa, utilitário e mãos) com ícone, durabilidade e tooltip; clicar numa armadura abre a troca de aparência (prévia local, integração opcional com `Deadheim.TransmogApi`).
- Coluna direita: somente Atributos (vida, stamina, Eitr, armadura, velocidade de movimento, peso).
- Escala local ajustável em `BepInEx/config/Deadheim.InventoryUI.cfg`.

## O que não altera

- Tamanho, conteúdo, posições ou capacidade do inventário.
- Itens equipados, atalhos ou salvamento do personagem ao abrir, navegar ou fechar a interface.
- Regras do servidor para VIP, Alfaiate ou transmog. A troca visual é validada pelo mod principal.

Não inclui linhas extras, espaços de equipamento adicionais, loadouts, favoritos, estatísticas além dos Atributos nem dependência do AzuEPI.

## Instalação

Instale `DeadheimInventoryUI.dll` em `BepInEx/plugins` no cliente. O mod principal Deadheim deve estar instalado para usar a aparência; sem ele, o resto continua disponível. Não é necessário instalar este plugin no servidor dedicado.

Se `AzuExtendedPlayerInventory` estiver presente, o plugin não ativa a própria UI para evitar disputar o mesmo painel.

## Compilar

```powershell
dotnet build DeadheimInventoryUI.csproj -c Release
```

Por padrão compila contra `D:\valheim-ref`; outra árvore pode ser indicada com `VALHEIM_PATH`.
