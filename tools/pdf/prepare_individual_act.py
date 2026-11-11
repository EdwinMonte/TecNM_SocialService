"""Prepare the individual act from a Word-exported original PDF; never edit the DOCX."""
from pathlib import Path
import hashlib
import json
import sys
from pypdf import PdfReader, PdfWriter
from pypdf.generic import ContentStream

ROOT = Path(__file__).resolve().parents[2]
RESOURCES = ROOT / 'backend/Resources/Documents'


def prepare(source):
    reader = PdfReader(source)
    assert len(reader.pages) == 1
    page = reader.pages[0]
    stream = ContentStream(page.get_contents(), reader)
    operations = []
    y = 0
    for operands, operator in stream.operations:
        if operator == b'Tm':
            y = float(operands[5])
        if operator == b're' and 465 < float(operands[1]) < 555 and float(operands[3]) < 2:
            continue
        # Remove actual text operators, including merge values and historical authorities.
        if operator in (b'Tj', b'TJ', b"'", b'"') and (465 < y < 555 or 360 < y < 390 or 120 < y < 155):
            continue
        operations.append((operands, operator))
    stream.operations = operations
    page.replace_contents(stream)
    writer = PdfWriter()
    writer.add_page(page, excluded_keys=['/Annots', '/AA', '/Metadata'])
    writer.add_metadata({})
    target = RESOURCES / 'templates/ss-individual-act.pdf'
    writer.write(target)
    text = PdfReader(target).pages[0].extract_text()
    assert all(value not in text for value in ['NOMBRE', 'MATRICULA', '2025', 'RIOJAS', 'GUERRERO'])
    def field(key, x, y, width, height, size, minimum, alignment):
        return dict(key=key, x=x, y=y, width=width, height=height,
                    fontSize=size, minFontSize=minimum, alignment=alignment)
    manifest = RESOURCES / 'manifest.json'
    entries = json.loads(manifest.read_text(encoding='utf-8'))
    entries['ss-individual-act'] = dict(
        file='templates/ss-individual-act.pdf', version='1', font='Arial',
        sha256=hashlib.sha256(target.read_bytes()).hexdigest(), pages=1,
        pageSizes=[dict(width=float(page.mediabox.width), height=float(page.mediabox.height))],
        fields=[field('actBody', 56.6, 233, 500, 96, 12.96, 10, 'justify'),
                field('actDate', 56.6, 398, 500, 48, 12.96, 11, 'justify'),
                field('outreachAuthority', 62, 633, 240, 50, 9, 8, 'center'),
                field('directorAuthority', 330, 633, 240, 50, 9, 8, 'center')])
    manifest.write_text(json.dumps(entries, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')


if __name__ == '__main__':
    prepare(Path(sys.argv[1]))
