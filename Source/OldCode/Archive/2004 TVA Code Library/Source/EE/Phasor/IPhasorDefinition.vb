'***********************************************************************
'  IPhasorDefinition.vb - Phasor data definition interface
'  Copyright � 2005 - TVA, all rights reserved
'
'  Build Environment: VB.NET, Visual Studio 2003
'  Primary Developer: James R Carroll, System Analyst [WESTAFF]
'      Office: COO - TRNS/PWR ELEC SYS O, CHATTANOOGA, TN - MR 2W-C
'       Phone: 423/751-2827
'       Email: jrcarrol@tva.gov
'
'  Code Modification History:
'  ---------------------------------------------------------------------
'  02/18/2005 - James R Carroll
'       Initial version of source generated
'
'***********************************************************************

Namespace EE.Phasor

    ' This class represents the protocol independent interface of a phasor value definition.
    Public Interface IPhasorDefinition

        Inherits IChannelDefinition

        Property [Type]() As PhasorType

        Property VoltageReference() As IPhasorDefinition

        Property CalFactor() As Double

        ReadOnly Property MaximumCalFactor() As Int32

    End Interface

End Namespace