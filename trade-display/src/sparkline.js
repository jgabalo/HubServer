// Minimal canvas sparkline, sized for the device pixel ratio.

export function drawSparkline(canvas, points, color) {
    const ratio = window.devicePixelRatio || 1;
    const width = canvas.clientWidth;
    const height = canvas.clientHeight;
    if (width === 0 || height === 0) return;

    if (canvas.width !== Math.round(width * ratio) || canvas.height !== Math.round(height * ratio)) {
        canvas.width = Math.round(width * ratio);
        canvas.height = Math.round(height * ratio);
    }

    const ctx = canvas.getContext('2d');
    ctx.setTransform(ratio, 0, 0, ratio, 0, 0);
    ctx.clearRect(0, 0, width, height);
    if (points.length < 2) return;

    let min = Infinity;
    let max = -Infinity;
    for (const p of points) {
        if (p < min) min = p;
        if (p > max) max = p;
    }
    const range = max - min || 1;
    const pad = 2;
    const x = i => (i / (points.length - 1)) * width;
    const y = p => pad + (1 - (p - min) / range) * (height - pad * 2);

    ctx.beginPath();
    points.forEach((p, i) => (i === 0 ? ctx.moveTo(x(i), y(p)) : ctx.lineTo(x(i), y(p))));
    ctx.strokeStyle = color;
    ctx.lineWidth = 1.5;
    ctx.lineJoin = 'round';
    ctx.stroke();

    // Soft fill under the line.
    ctx.lineTo(width, height);
    ctx.lineTo(0, height);
    ctx.closePath();
    ctx.globalAlpha = 0.12;
    ctx.fillStyle = color;
    ctx.fill();
    ctx.globalAlpha = 1;
}
