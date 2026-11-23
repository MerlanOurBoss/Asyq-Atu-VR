using UnityEngine;

namespace AsykAtu
{
    /// <summary>
    /// Орда — круг на земле, в центре которого стоит кон. Асык выбит, если лежит за кругом.
    /// Линия броска — на расстоянии throwDistance от центра, со стороны −Z объекта (игрок смотрит вдоль +Z).
    /// </summary>
    public class Orda : MonoBehaviour
    {
        [Tooltip("Радиус круга, м.")]
        public float radius = 0.75f;
        [Tooltip("От центра круга до линии броска, м. По спортивным правилам 6 м, для тренировки меньше.")]
        public float throwDistance = 3f;
        public float throwLineWidth = 1.5f;

        [Header("Разметка (необязательно)")]
        public LineRenderer circle;
        public LineRenderer throwLine;
        [Range(16, 128)] public int circleSegments = 64;

        public Vector3 Center => transform.position;

        public bool IsOutside(Vector3 point)
        {
            Vector3 d = point - Center;
            d.y = 0f;
            return d.sqrMagnitude > radius * radius;
        }

        /// <summary>Место i-го асыка в коне: ряд поперёк направления броска.</summary>
        public Vector3 KonPoint(int index, int count, float spacing) =>
            Center + transform.right * ((index - (count - 1) * 0.5f) * spacing);

        void Awake() => Redraw();
        void OnValidate() => Redraw();

        public void Redraw()
        {
            if (circle != null)
            {
                LayFlat(circle);
                circle.loop = true;
                circle.positionCount = circleSegments;
                for (int i = 0; i < circleSegments; i++)
                {
                    float a = i * Mathf.PI * 2f / circleSegments;
                    circle.SetPosition(i, Local(circle, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius)));
                }
            }
            if (throwLine != null)
            {
                LayFlat(throwLine);
                throwLine.loop = false;
                throwLine.positionCount = 2;
                throwLine.SetPosition(0, Local(throwLine, new Vector3(-throwLineWidth * 0.5f, 0f, -throwDistance)));
                throwLine.SetPosition(1, Local(throwLine, new Vector3(throwLineWidth * 0.5f, 0f, -throwDistance)));
            }
        }

        /// <summary>
        /// Линия лежит плашмя на земле: лента смотрит вверх, а не на камеру.
        /// Иначе с высоты глаз половина её толщины уходит под землю.
        /// </summary>
        void LayFlat(LineRenderer line)
        {
            line.useWorldSpace = false;
            line.alignment = LineAlignment.TransformZ;
            line.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // локальная Z смотрит вверх
        }

        /// <summary>Точка в координатах орды → в координаты линии, чуть над землёй, чтобы не мерцала.</summary>
        Vector3 Local(LineRenderer line, Vector3 ordaPoint)
        {
            const float lift = 0.003f;
            Vector3 world = transform.TransformPoint(ordaPoint + Vector3.up * lift);
            return line.transform.InverseTransformPoint(world);
        }
    }
}
