#!/usr/bin/env python3
"""Alta de socio via CLI (usa la misma logica que la API REST)."""
import argparse
import sys

from database import Database
from socio_service import crear_socio
from zkteco_protocol import DEFAULT_DEVICE_SN
from config import Colors


def main():
    parser = argparse.ArgumentParser(description='Alta de socio en portero ZKTeco')
    parser.add_argument('--nombre', default='magollego')
    parser.add_argument('--cedula', default='51498995')
    parser.add_argument('--celular', default='092331019')
    parser.add_argument('--sn', default=DEFAULT_DEVICE_SN, help='Serial del dispositivo')
    args = parser.parse_args()

    try:
        db = Database()
        result = crear_socio(db, args.nombre, args.cedula, args.celular, device_sn=args.sn)
        print(f"""
{Colors.OKGREEN}SOCIO ENCOLADO PARA EL PORTERO
{Colors.ENDC}
  Nombre:      {result['nombre']}
  Cedula/PIN:  {result['pin']}
  Celular:     {result['celular']}
  Dispositivo: {result['device_sn']}
  Comando ID:  {result['command_id']}

{Colors.WARNING}El portero lo recibira en el proximo getrequest (~10 seg).
Despues el socio debe registrar su rostro en el equipo.
{Colors.ENDC}
""")
    except Exception as e:
        print(f"{Colors.FAIL}Error: {e}{Colors.ENDC}")
        sys.exit(1)


if __name__ == '__main__':
    main()
