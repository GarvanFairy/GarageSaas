"""Verify application-generated synthetic PDF samples after the .NET sample tests."""
from pathlib import Path
import sys
from pypdf import PdfReader

root = Path(sys.argv[1] if len(sys.argv) > 1 else 'output/pdf')
for name, title in [('quote.pdf', 'QUOTE'), ('invoice.pdf', 'INVOICE'), ('multipage-quote.pdf', 'QUOTE')]:
    pdf = PdfReader(root / name)
    texts = [page.extract_text() for page in pdf.pages]
    text = '\n'.join(texts)
    for expected in [title, "O'Neill Motor Services", 'Se\u00e1n \u00d3 Briain', '261-D-123',
                     'IE1234567A', '\u20ac128.00', '\u20ac30.08', '\u20ac158.08', 'VAT (23.5%)']:
        assert expected in text, (name, ascii(expected))
    for index, page in enumerate(pdf.pages):
        assert abs(float(page.mediabox.width) - 595.28) < .1
        assert abs(float(page.mediabox.height) - 841.89) < .1
        assert f'Page {index+1} of {len(pdf.pages)}' in texts[index]
    if name == 'multipage-quote.pdf':
        assert len(pdf.pages) >= 3
        for index in range(1, 81):
            assert f'Service operation {index}:' in text
        assert 'End of long description.' in text
        assert 'This is a quote, not an invoice.' in text
    elif title == 'INVOICE':
        assert 'Payment status: Paid' in text
        assert 'INV-2026-000001' in text
    else:
        assert 'Q-100' in text and 'This is a quote, not an invoice.' in text
    print(f'{name}: {len(pdf.pages)} A4 page(s), identity, amounts, text and pagination verified')
