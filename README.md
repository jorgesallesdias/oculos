# 🥽 Cápsula das Terras Raras - CETEM VR

Experiência imersiva em Realidade Virtual desenvolvida para o **CETEM (Centro de Tecnologia Mineral)**. Projetada especificamente para **feiras, exposições e eventos institucionais**, voltada ao público jovem e adolescente para apresentar de forma interativa o papel dos minerais estratégicos e das terras raras no nosso cotidiano e na transição energética.

---

## 🎯 Visão Geral da Experiência

O visitante é transportado para uma **bancada de laboratório futurista do CETEM**, onde é recebido pelo mascote virtual da instituição e pode inspecionar **4 amostras de minerais estratégicos**:

1. **Neodímio ($\text{Nd}$):** Terras raras aplicadas em superímãs de fones de ouvido e turbinas eólicas.
2. **Lítio ($\text{Li}$):** Elemento essencial para baterias de veículos elétricos, celulares e transição energética.
3. **Nióbio ($\text{Nb}$):** Superligas metálicas de alta resistência para aeroespacial, satélites e infraestrutura.
4. **Quartzo / Silício de Alta Pureza ($\text{SiO}_2 / \text{Si}$):** Base para microchips, semicondutores e células fotovoltaicas (painéis solares).

### Características de Operação em Estande (Kiosk Mode)
- **Zero Controles Físicos (Hands-Free):** Para evitar perda de controles, queda de bateria ou atrasos na fila, toda a interação ocorre via **Gaze Interactor** (olhar fixo por `2.0` segundos com retícula radial de carregamento).
- **Sessão Controlada (90 a 120 segundos):** A experiência possui controle automático de tempo ou encerra após a exploração de **3 minerais**, garantindo alta rotatividade na fila do evento.
- **Ponte Virtual-Real (Phygital):** Ao final, o visitante é convidado a retirar os óculos e tocar nas amostras minerais reais disponíveis na bancada física do estande do CETEM.
- **Auto-Reset:** A cena reinicia sozinha ao término do ciclo, pronta para o próximo visitante sem necessidade de intervenção técnica.

---

## 🛠️ Requisitos Técnicos e Ambiente Unity

| Componente | Especificação Recomendada |
| :--- | :--- |
| **Unity Version** | **Unity 2022.3 LTS** (ou Unity 6 LTS) |
| **Render Pipeline** | **Universal Render Pipeline (URP)** otimizado para Mobile VR |
| **Target Hardware** | **Meta Quest 2** (5 unidades em modo Standalone Android APK) |
| **XR Architecture** | **OpenXR Plugin** + **XR Interaction Toolkit (XRI)** |
| **linguagem** | C# (.NET Standard 2.1) |

### Pacotes Essenciais (`Package Manager`)
- `com.unity.xr.openxr` (OpenXR Plugin com perfil *Oculus Touch Controller* / *Meta Quest Support*)
- `com.unity.xr.interaction.toolkit` (XR Interaction Toolkit)
- `com.unity.render-pipelines.universal` (URP)
- `com.unity.textmeshpro` (UI de alta nitidez em VR)

---

## 📦 Estrutura de Pastas do Projeto

```text
oculos/
├── .gitattributes
├── .gitignore
├── README.md
├── DOCS/
│   └── PLANEJAMENTO.md
└── Assets/
    └── _Project/
        ├── Audio/                  # Narrações do mascote (.wav) e SFX
        ├── Materials/              # Shaders URP Lit/Unlit e efeito holográfico
        ├── Models/                 # Pedestais, cristais (.fbx) e mascote
        ├── Prefabs/                # Hologramas de aplicação e pedestais completos
        ├── Scenes/                 # MainLab_CETEM_VR.unity
        ├── ScriptableObjects/      # Instâncias de MineralData (Neodímio, Lítio, etc.)
        └── Scripts/
            ├── MineralData.cs
            ├── GazeInteractableMineral.cs
            ├── ExperienceManager.cs
            └── FloatingRotator.cs
```

---

## 🚀 Instruções de Configuração e Build para Meta Quest 2

Para garantir **72Hz / 90Hz estáveis** sem enjoo (motion sickness) ou superaquecimento nos 5 óculos:

### 1. Build Settings (Android)
1. Acesse `File > Build Settings` e selecione a plataforma **Android** (`Switch Platform`).
2. Defina **Texture Compression** como **`ASTC`** (essencial para a GPU Adreno XR2 do Quest 2).
3. Em `Run Device`, selecione o Meta Quest 2 conectado via USB (com *Developer Mode* ativado).

### 2. Player Settings (`Edit > Project Settings > Player`)
- **Scripting Backend:** `IL2CPP`
- **Target Architectures:** Marque **apenas** `ARM64` (desmarque ARMv7).
- **Color Space:** `Linear` (recomendado para iluminação realista e brilho de cristais).
- **Graphics APIs:** `Vulkan` (ou `OpenGLES3`, removendo APIs legadas).
- **Minimum API Level:** `Android 10.0 (API level 29)` ou superior (`API 32`).

### 3. XR Plug-in Management & OpenXR
- Ative **OpenXR** na aba Android.
- Em `OpenXR > Features`, habilite **Meta Quest Support**.
- Em **URP Asset Settings**:
  - **MSAA (Anti-Aliasing):** `4x` (obrigatório em VR para reduzir serrilhado nos textos e hologramas).
  - **HDR:** Desativado (usar Bloom leve ou Shaders aditivos para os hologramas para economizar GPU).
  - **Render Scale:** `1.0` a `1.2`.

---

## 🎪 Guia Rápido para a Equipe de Estande (Operação na Feira)

1. **Antes de abrir o estande:**
   - Ligue os **5 Meta Quest 2** e verifique se a bateria está em 100% (mantenha cabos USB-C ou powerbanks prontos para recarga nos intervalos).
   - Desative os controles físicos (remova as pilhas dos controles e guarde-os na caixa) para que o Quest opere sem aviso de controle pareado ou em modo *Hands-Free*.
   - Delimite o **Sistema Guardião (modo Estacionário / Sentado)** exatamente na posição em que o visitante ficará em pé/sentado em frente à bancada real.
   - Abra o aplicativo **"Cápsula das Terras Raras - CETEM VR"**.

2. **Durante o atendimento ao visitante:**
   - Higienize a espuma facial de silicone com lenço umedecido sem álcool antes de cada visitante.
   - Posicione os óculos no rosto do jovem e instrua: *"Basta olhar para o cristal que você quer conhecer e segurar o olhar no círculo por 2 segundos!"*.
   - **Não é necessário apertar nenhum botão:** O aplicativo detecta o olhar no início e encerra sozinho após ~90 segundos (ou 3 minerais explorados).

3. **Finalização (Ponte com a Bancada Real):**
   - Quando o visitante ouvir a mensagem final de conclusão, ajude-o a retirar o Meta Quest 2 e mostre a **amostra mineral real** na bancada física correspondente ao que ele acabou de ver no laboratório virtual!
   - Após `10 segundos`, os óculos já estarão automaticamente na tela inicial aguardando o próximo visitante da fila.

