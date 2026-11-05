# Deadheim Inventory — especificação inicial

## Objetivo

Criar um mod próprio do Deadheim inspirado na apresentação do AzuExtendedPlayerInventory: uma interface de equipamento organizada, agradável de usar e integrada ao estilo visual do Valheim, com recursos extras selecionados e sem depender do AzuEPI ou de outra biblioteca de interface.

O mod será um projeto separado do mod principal Deadheim. A interface será construída com Unity UI e recursos visuais do próprio jogo sempre que possível.

## Princípios

- Priorizar leitura clara, poucos cliques e navegação por teclado, mouse e controle.
- Preservar itens e personagens existentes. Não apagar nem descartar itens automaticamente.
- Manter os recursos desligáveis e configuráveis pelo servidor quando afetarem PvP ou equilíbrio.
- Não criar uma dependência de AzuEPI, Jötunn ou outro mod de inventário.
- Não prometer ausência absoluta de bugs: mudanças no inventário do personagem exigem validação específica e lançamento gradual.
- Separar a apresentação visual do código que altera inventário, equipamento ou aparência.

## Experiência proposta

### Tela principal

- Integrar o layout à própria tela de inventário do Valheim: ao pressionar **Tab**, o inventário abre pelo fluxo normal do jogo já mostrando a interface do mod, no mesmo estilo de integração do AzuEPI.
- Não abrir uma janela independente nem exigir outro botão para chegar à interface; preservar o fluxo normal de abertura e fechamento do inventário.
- Não abrir por cima de menus, chat ou outra interface que esteja em uso.
- Usar o painel de criação do inventário como área de abas: Equipamento, Aparência, Atalhos e acesso de volta ao Artesanato nativo.
- Reorganização visual do inventário, equipamento e atalhos rápidos em um painel coeso.
- Abas ou seções claras para Inventário, Equipamento e Aparência.
- Ícones e tipografia do Valheim, espaços consistentes e destaque discreto para seleção, item equipado e durabilidade baixa.
- Redimensionamento responsivo para resoluções diferentes, com escala configurável.
- Tooltips nativos ou equivalentes para detalhes de itens.
- Navegação com mouse/teclado e suporte a gamepad.
- Alternativa compacta para quem preferir uma tela próxima do inventário clássico.

### Recursos extras previstos

1. **Equipamento** — apresentar os espaços existentes de equipamento de forma clara. A primeira versão não adiciona slots nem muda as regras/capacidade do inventário.
2. **Atalhos rápidos** — painel HUD opcional para os atalhos que o jogo já suporta, com posição e escala configuráveis. Não consumir nem mover itens ao configurar ou reposicionar o painel.
3. **Aparência (vanity)** — trocar a aparência visual de equipamento sem alterar seus atributos ou os itens equipados. Desativável pelo servidor e testado em multiplayer.
4. **Prévia do personagem** — visualização opcional do personagem e da aparência atual. Deve poder ser ocultada para reduzir custo visual.

## Escopo da primeira entrega

### Fase A — base visual e leitura segura

- Criar plugin independente e abrir/fechar sua interface sem substituir os dados do inventário.
- Apresentar os itens e equipamentos atuais do personagem.
- Construir a navegação, tooltips, escala, estados visuais e controles.
- Validar abertura integrada por Tab, fechamento pelo fluxo normal do inventário, troca de foco, abertura de menus e compatibilidade com a interface do Valheim.

### Integração de aparência

- Quando o mod principal Deadheim estiver instalado, usar sua API de Transmog por integração opcional.
- A prévia é local; pedidos de aplicação continuam sujeitos às regras de VIP, Alfaiate e validação do servidor.
- Sem a API compatível, ocultar as ações de alteração e informar que o recurso não está disponível.

### Fase B — recursos visuais e atalhos

- Painel para atalhos rápidos já existentes, sem aumentar a quantidade de espaços.
- Prévia do personagem.
- Preferências visuais locais.

### Fase C — aparência

- Vanity sincronizada, sem substituir nem mover itens reais.

O recurso da Fase C deve poder ser desligado isoladamente e passar pela validação de salvamento, reconexão, morte, túmulo, PvP e presença de outros mods que mexam em equipamento ou aparência.

## Regras de segurança dos itens

- Nunca remover, sobrescrever ou largar itens como efeito colateral de abrir ou fechar a interface.
- A interface não altera o tamanho nem a composição do inventário.
- A troca de aparência só altera a representação visual e não substitui os itens equipados.
- Evitar alterações diretas em campos privados. Quando inevitável, usar AccessTools/reflexão e confirmar membros contra o assembly da versão alvo, conforme as regras do workspace.
- Registrar erros de forma útil sem despejar conteúdo privado do personagem no log.

## Multiplayer e configuração

- Separar configurações cosméticas locais das regras de inventário controladas pelo servidor.
- A aparência sincronizada exige uma estratégia explícita de cliente/servidor.
- O servidor poderá desativar recursos que afetem equilíbrio PvP.
- Compatibilidade com outros mods de inventário não será presumida. Detectar conflitos conhecidos e desativar apenas o recurso conflitante, com mensagem clara.
- A primeira versão deve registrar quais mods/versões relevantes foram detectados para facilitar diagnóstico.

## Direção visual

- Usar o estilo nativo de Valheim como base, com identidade Deadheim nos detalhes e estados selecionados.
- Evitar excesso de painéis, textos persistentes e animações longas.
- Animações curtas e discretas, opção de reduzir movimento e sons de interação compatíveis com o jogo.
- Contraste suficiente para ícones, quantidades e durabilidade em diferentes resoluções.
- Manter informação importante visível sem cobrir o personagem ou a janela de criação.

## Critérios de aceite

- Interface abre e fecha sem deixar cursor, bloqueio de entrada ou painel preso.
- Abrir, navegar e fechar a UI não altera a lista, quantidade, posição, qualidade ou estado equipado dos itens.
- Cada recurso extra pode ser ativado/desativado conforme sua categoria de configuração.
- Trocar aparência não altera quantidade, posição, qualidade ou estado equipado dos itens.
- Personagem existente continua carregando com os mesmos itens antes e depois da instalação e remoção do mod, dentro do escopo de recursos efetivamente ativados.
- UI continua utilizável em resoluções e escalas diferentes, com mouse/teclado e controle.
- Erros de compatibilidade são apresentados sem travar a interface nem corromper dados.

## Fora do escopo inicial

- Comandos de console para apagar, largar ou reparar tudo.
- Automação de armazenamento ou transferência de itens para baús.
- Integração que exija que outros autores adotem uma API própria.
- Clonar integralmente a implementação ou os recursos gráficos do AzuEPI.

## Questões técnicas a fechar antes da implementação

- Compilar e validar contra Valheim **1.0.21**, usando os assemblies correspondentes à instalação alvo.
- Quais mods ativos no perfil também alteram inventário, equipamento ou atalhos.
- Quais recursos o servidor quer habilitar por padrão para todos os jogadores.
- Quantidade, teclas e apresentação dos atalhos rápidos já existentes no jogo.
- Se vanity será permitida em todas as áreas PvP ou sujeita a bloqueios do servidor.

