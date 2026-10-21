using UnityEngine;

namespace CetemVR
{
    /// <summary>
    /// ScriptableObject que armazena os dados estáticos e assets de cada mineral estratégico
    /// da experiência "Cápsula das Terras Raras - CETEM VR".
    /// Permite que a equipe configure novos minérios diretamente pelo Inspector da Unity.
    /// </summary>
    [CreateAssetMenu(fileName = "NovoMineralData", menuName = "CETEM VR/Dados do Mineral", order = 1)]
    public class MineralData : ScriptableObject
    {
        [Header("Identificação Científica")]
        [Tooltip("Nome de exibição do mineral (ex: Neodímio, Lítio, Nióbio, Quartzo / Silício).")]
        public string mineralName = "Neodímio";

        [Tooltip("Símbolo químico ou fórmula exibida no painel holográfico (ex: Nd, Li, Nb, SiO2).")]
        public string chemicalSymbol = "Nd";

        [Tooltip("Categoria ou subtítulo rápido do mineral (ex: Terras Raras, Transição Energética).")]
        public string categorySubtitle = "Terras Raras - Superímãs";

        [Tooltip("Resumo curto exibido na interface 3D próxima ao pedestal.")]
        [TextArea(3, 5)]
        public string shortDescription = "Essencial para superímãs de fones de ouvido e turbinas de energia eólica.";

        [Header("Áudio e Narração do Mascote")]
        [Tooltip("Áudio da explicação do mascote para este mineral (~20 segundos).")]
        public AudioClip mascotExplanationClip;

        [Tooltip("Efeito sonoro opcional disparado no momento em que o mineral é ativado pelo olhar.")]
        public AudioClip activationSfx;

        [Header("Visual e Holograma de Aplicação")]
        [Tooltip("Prefab 3D do holograma que representa a aplicação tecnológica (ex: Turbina, Bateria, Foguete, Microchip).")]
        public GameObject applicationHologramPrefab;

        [Tooltip("Cor temática de destaque para iluminação, partículas, borda e retícula do olhar.")]
        [ColorUsage(true, true)]
        public Color highlightColor = new Color(0f, 0.94f, 1f, 1f);
    }
}

