using UnityEngine;

namespace Yaoron.Avatar
{
    /// <summary>
    /// 腕 2 ボーン分の解析 IK。ランタイムに読み込む VRM へ Animation Rigging のリグ構造を
    /// 後付けするのは重いので、必要最小限の解を自前で持つ (設計書 §6 のポーズ復元)。
    /// 余弦定理で肩と肘の角度を決め、最後に肘の向きを hint に合わせる標準的な解法。
    /// </summary>
    public static class TwoBoneIk
    {
        /// <param name="upper">上腕</param>
        /// <param name="lower">前腕</param>
        /// <param name="end">手</param>
        /// <param name="target">手先の目標位置</param>
        /// <param name="hint">肘を向けたい参照点</param>
        /// <param name="weight">0 で元のポーズ、1 で完全に IK</param>
        public static void Solve(Transform upper, Transform lower, Transform end,
                                 Vector3 target, Vector3 hint, float weight = 1f)
        {
            if (upper == null || lower == null || end == null) return;
            weight = Mathf.Clamp01(weight);
            if (weight <= 0f) return;

            var upperOriginal = upper.rotation;
            var lowerOriginal = lower.rotation;

            Vector3 a = upper.position, b = lower.position, c = end.position;
            Vector3 ab = b - a, cb = b - c, ac = c - a, at = target - a;

            float lab = ab.magnitude, lcb = cb.magnitude;
            if (lab <= 1e-5f || lcb <= 1e-5f) return;
            float lat = Mathf.Clamp(at.magnitude, 1e-4f, lab + lcb - 1e-4f);

            float acAb0 = Angle(ac, ab);
            float baBc0 = Angle(a - b, c - b);
            float acAt0 = Angle(ac, at);

            float acAb1 = Mathf.Acos(Mathf.Clamp((lcb * lcb - lab * lab - lat * lat) / (-2f * lab * lat), -1f, 1f));
            float baBc1 = Mathf.Acos(Mathf.Clamp((lat * lat - lab * lab - lcb * lcb) / (-2f * lab * lcb), -1f, 1f));

            Vector3 bendAxis = Vector3.Cross(ac, ab);
            if (bendAxis.sqrMagnitude < 1e-8f) bendAxis = Vector3.Cross(at, hint - a);
            if (bendAxis.sqrMagnitude < 1e-8f) return;
            bendAxis.Normalize();

            Vector3 swingAxis = Vector3.Cross(ac, at);
            if (swingAxis.sqrMagnitude > 1e-8f) swingAxis.Normalize();

            upper.rotation = Quaternion.AngleAxis((acAb1 - acAb0) * Mathf.Rad2Deg, bendAxis) * upper.rotation;
            lower.rotation = Quaternion.AngleAxis((baBc1 - baBc0) * Mathf.Rad2Deg, bendAxis) * lower.rotation;
            if (swingAxis.sqrMagnitude > 1e-8f)
                upper.rotation = Quaternion.AngleAxis(acAt0 * Mathf.Rad2Deg, swingAxis) * upper.rotation;

            AlignElbowToHint(upper, lower, a, at, hint - a);

            if (weight < 1f)
            {
                upper.rotation = Quaternion.Slerp(upperOriginal, upper.rotation, weight);
                lower.rotation = Quaternion.Slerp(lowerOriginal, lower.rotation, weight);
            }
        }

        /// <summary>肩〜手の軸まわりに肘を回して、ねじれを hint 方向に決める。</summary>
        static void AlignElbowToHint(Transform upper, Transform lower, Vector3 shoulder, Vector3 toTarget, Vector3 toHint)
        {
            var axis = toTarget.normalized;
            var current = Vector3.ProjectOnPlane(lower.position - shoulder, axis);
            var wanted = Vector3.ProjectOnPlane(toHint, axis);
            if (current.sqrMagnitude < 1e-6f || wanted.sqrMagnitude < 1e-6f) return;
            upper.rotation = Quaternion.FromToRotation(current, wanted) * upper.rotation;
        }

        static float Angle(Vector3 from, Vector3 to)
            => Mathf.Acos(Mathf.Clamp(Vector3.Dot(from.normalized, to.normalized), -1f, 1f));
    }
}
