(() => {
  const button = document.getElementById('admin-collapse');
  if (!button) return;
  button.addEventListener('click', () => {
    const compact = document.body.classList.toggle('admin-compact');
    const label = compact ? 'Mở rộng menu' : 'Thu gọn menu';
    button.setAttribute('aria-expanded', String(!compact));
    button.setAttribute('aria-label', label);
    button.title = label;
    button.querySelector('.nav-label').textContent = label;
    button.querySelector('.nav-symbol').textContent = compact ? '⇥' : '⇤';
  });
})();
