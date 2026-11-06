# Deadheim Inventory UI

Plugin cliente para Valheim 1.0.21. Mostra, ao abrir o inventário (Tab), uma ficha do personagem: só exibição, sem botões.

## O que inclui

- Painel único, sem abas, à esquerda do painel de crafting (que permanece nativo).
- Coluna esquerda: slots de equipamento (cabeça, peito, pernas, capa, utilitário, berloque, mãos e munição) com ícone, durabilidade e tooltip. Se a peça tiver aparência aplicada no Deadheim, o ícone mostra a aparência (leitura de `Deadheim.TransmogApi`); nada pode ser trocado por aqui.
- Centro: prévia 3D do personagem como os outros o veem.
- Coluna direita: somente Atributos (vida, stamina, Eitr, armadura, velocidade de movimento, peso).
- Escala local ajustável em `BepInEx/config/Deadheim.InventoryUI.cfg`.

## O que não altera

- Tamanho, conteúdo, posições ou capacidade do inventário.
- Itens equipados, atalhos ou salvamento do personagem ao abrir, navegar ou fechar a interface.
- Aparência/transmog: o mod só lê o que já foi aplicado pelo mod principal.

Não inclui linhas extras, espaços de equipamento adicionais, loadouts, favoritos, estatísticas além dos Atributos nem dependência do AzuEPI.

## Instalação

Instale `DeadheimInventoryUI.dll` em `BepInEx/plugins` no cliente. Sem o mod principal Deadheim, os ícones mostram a peça real e o resto continua igual. Não é necessário instalar este plugin no servidor dedicado.

Se `AzuExtendedPlayerInventory` estiver presente, o plugin não ativa a própria UI para evitar disputar o mesmo painel.

## Compilar

```powershell
dotnet build DeadheimInventoryUI.csproj -c Release
```

Por padrão compila contra `D:\valheim-ref`; outra árvore pode ser indicada com `VALHEIM_PATH`.
